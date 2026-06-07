import AVFoundation
import Accelerate
import Combine

/// Continuously monitors the microphone via FFT.
/// When snoring is detected, records a clip (pre-buffer + active snoring) and saves it.
class AudioEngine: ObservableObject {

    // MARK: - Published state

    @Published var isMonitoring = false
    @Published var snoreEvents: [SnoreEvent] = []
    @Published var currentDB: Float = 0
    @Published var isSnoring = false

    // MARK: - Configuration

    /// Frequency range typical for snoring (Hz)
    private let snoreFreqMin: Float = 100
    private let snoreFreqMax: Float = 500
    /// Minimum power in snore band to trigger detection (tunable)
    private let detectionThreshold: Float = 0.015
    /// Consecutive frames that must exceed threshold to confirm snoring
    private let confirmFrames = 8
    /// Seconds of audio kept in pre-roll buffer before detection
    private let preRollSeconds: Double = 3
    /// Stop recording after this many silent frames post-snoring
    private let silenceFramesToStop = 30

    // MARK: - AVFoundation objects

    private let engine = AVAudioEngine()
    private var recordingFile: AVAudioFile?

    // MARK: - Internal state

    private var preRollBuffer: [[Float]] = []
    private var preRollSampleRate: Double = 44100
    private var consecutiveSnoreFrames = 0
    private var consecutiveSilentFrames = 0
    private var isRecording = false

    // MARK: - FFT setup

    private let fftSize = 4096
    private var fftSetup: FFTSetup?
    private var window: [Float] = []

    // MARK: - Init

    init() {
        fftSetup = vDSP_create_fftsetup(vDSP_Length(log2(Float(fftSize))), FFTRadix(kFFTRadix2))
        window = [Float](repeating: 0, count: fftSize)
        vDSP_hann_window(&window, vDSP_Length(fftSize), Int32(vDSP_HANN_NORM))
    }

    deinit {
        if let setup = fftSetup { vDSP_destroy_fftsetup(setup) }
    }

    // MARK: - Public API

    func startMonitoring() {
        guard !isMonitoring else { return }
        configureAudioSession()

        let input = engine.inputNode
        let format = input.outputFormat(forBus: 0)
        preRollSampleRate = format.sampleRate

        input.installTap(onBus: 0, bufferSize: AVAudioFrameCount(fftSize), format: format) { [weak self] buffer, _ in
            self?.process(buffer: buffer)
        }

        do {
            try engine.start()
            DispatchQueue.main.async { self.isMonitoring = true }
        } catch {
            print("AudioEngine start error: \(error)")
        }
    }

    func stopMonitoring() {
        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
        finaliseRecording()
        DispatchQueue.main.async { self.isMonitoring = false }
    }

    // MARK: - Audio session

    private func configureAudioSession() {
        let session = AVAudioSession.sharedInstance()
        do {
            try session.setCategory(.playAndRecord,
                                    mode: .measurement,
                                    options: [.defaultToSpeaker, .allowBluetooth])
            try session.setActive(true)
        } catch {
            print("AVAudioSession error: \(error)")
        }
    }

    // MARK: - Frame processing

    private func process(buffer: AVAudioPCMBuffer) {
        guard let channelData = buffer.floatChannelData?[0] else { return }
        let frameCount = Int(buffer.frameLength)
        let samples = Array(UnsafeBufferPointer(start: channelData, count: frameCount))

        // dB level
        var rms: Float = 0
        vDSP_rmsqv(samples, 1, &rms, vDSP_Length(frameCount))
        let db = 20 * log10(max(rms, 1e-9))
        DispatchQueue.main.async { self.currentDB = db }

        // FFT snore band power
        let power = snoringBandPower(samples: samples)
        let detected = power > detectionThreshold

        // Update pre-roll ring buffer (keeps ~preRollSeconds of frames)
        let maxPreRollFrames = Int((preRollSeconds * preRollSampleRate) / Double(fftSize)) + 1
        preRollBuffer.append(samples)
        if preRollBuffer.count > maxPreRollFrames { preRollBuffer.removeFirst() }

        if detected {
            consecutiveSilentFrames = 0
            consecutiveSnoreFrames += 1
            if consecutiveSnoreFrames == confirmFrames {
                DispatchQueue.main.async { self.isSnoring = true }
                startRecordingIfNeeded(format: buffer.format)
            }
        } else {
            consecutiveSnoreFrames = 0
            if isRecording {
                consecutiveSilentFrames += 1
                if consecutiveSilentFrames >= silenceFramesToStop {
                    DispatchQueue.main.async { self.isSnoring = false }
                    finaliseRecording()
                }
            }
        }

        // Write to file if recording
        if isRecording, let file = recordingFile {
            try? file.write(from: buffer)
        }
    }

    // MARK: - FFT

    private func snoringBandPower(samples: [Float]) -> Float {
        var padded = samples
        if padded.count < fftSize { padded += [Float](repeating: 0, count: fftSize - padded.count) }
        let frame = Array(padded.prefix(fftSize))

        var windowed = [Float](repeating: 0, count: fftSize)
        vDSP_vmul(frame, 1, window, 1, &windowed, 1, vDSP_Length(fftSize))

        var real = windowed
        var imag = [Float](repeating: 0, count: fftSize)
        var splitComplex = DSPSplitComplex(realp: &real, imagp: &imag)

        guard let setup = fftSetup else { return 0 }
        vDSP_fft_zip(setup, &splitComplex, 1, vDSP_Length(log2(Float(fftSize))), FFTDirection(FFT_FORWARD))

        var magnitudes = [Float](repeating: 0, count: fftSize / 2)
        vDSP_zvabs(&splitComplex, 1, &magnitudes, 1, vDSP_Length(fftSize / 2))

        // Bin range for snore frequencies
        let binWidth = Float(preRollSampleRate) / Float(fftSize)
        let lowBin  = Int(snoreFreqMin / binWidth)
        let highBin = min(Int(snoreFreqMax / binWidth), fftSize / 2 - 1)

        let bandMags = Array(magnitudes[lowBin...highBin])
        var power: Float = 0
        vDSP_meanv(bandMags, 1, &power, vDSP_Length(bandMags.count))
        return power / Float(fftSize)
    }

    // MARK: - Recording helpers

    private func startRecordingIfNeeded(format: AVAudioFormat) {
        guard !isRecording else { return }
        isRecording = true

        let fileName = "snore_\(Int(Date().timeIntervalSince1970)).caf"
        let url = FileManager.default
            .urls(for: .documentDirectory, in: .userDomainMask)[0]
            .appendingPathComponent(fileName)

        do {
            recordingFile = try AVAudioFile(forWriting: url,
                                            settings: format.settings,
                                            commonFormat: .pcmFormatFloat32,
                                            interleaved: false)
            // Write pre-roll buffer into file first
            writePreRoll(format: format)
        } catch {
            print("Could not create recording file: \(error)")
            isRecording = false
        }
    }

    private func writePreRoll(format: AVAudioFormat) {
        guard let file = recordingFile else { return }
        for chunk in preRollBuffer {
            guard let buf = AVAudioPCMBuffer(pcmFormat: format,
                                             frameCapacity: AVAudioFrameCount(chunk.count)) else { continue }
            buf.frameLength = buf.frameCapacity
            chunk.withUnsafeBufferPointer { ptr in
                buf.floatChannelData?[0].update(from: ptr.baseAddress!, count: chunk.count)
            }
            try? file.write(from: buf)
        }
    }

    private func finaliseRecording() {
        guard isRecording, let file = recordingFile else { return }
        isRecording = false
        consecutiveSnoreFrames = 0
        consecutiveSilentFrames = 0

        let url = file.url
        recordingFile = nil

        let event = SnoreEvent(date: Date(), fileURL: url)
        DispatchQueue.main.async { self.snoreEvents.append(event) }
    }
}
