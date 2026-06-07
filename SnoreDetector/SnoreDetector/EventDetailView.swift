import SwiftUI
import AVFoundation

struct EventDetailView: View {
    let event: SnoreEvent
    @State private var player: AVAudioPlayer?
    @State private var isPlaying = false

    var body: some View {
        VStack(spacing: 24) {
            Image(systemName: "waveform")
                .resizable()
                .scaledToFit()
                .frame(height: 60)
                .foregroundColor(.indigo)

            Text(event.formattedDate)
                .font(.title3.bold())

            Button {
                togglePlayback()
            } label: {
                Label(isPlaying ? "停止" : "再生",
                      systemImage: isPlaying ? "stop.circle.fill" : "play.circle.fill")
                    .font(.headline)
                    .padding()
                    .frame(maxWidth: .infinity)
                    .background(Color.indigo)
                    .foregroundColor(.white)
                    .clipShape(RoundedRectangle(cornerRadius: 14))
            }

            ShareLink(item: event.fileURL) {
                Label("共有", systemImage: "square.and.arrow.up")
                    .font(.headline)
                    .padding()
                    .frame(maxWidth: .infinity)
                    .background(Color(.systemGray5))
                    .clipShape(RoundedRectangle(cornerRadius: 14))
            }

            Spacer()
        }
        .padding()
        .navigationTitle("録音詳細")
        .onDisappear { player?.stop() }
    }

    private func togglePlayback() {
        if isPlaying {
            player?.stop()
            isPlaying = false
        } else {
            do {
                try AVAudioSession.sharedInstance().setCategory(.playback)
                player = try AVAudioPlayer(contentsOf: event.fileURL)
                player?.play()
                isPlaying = true
            } catch {
                print("Playback error: \(error)")
            }
        }
    }
}
