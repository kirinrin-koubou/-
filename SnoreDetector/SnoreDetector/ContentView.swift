import SwiftUI

struct ContentView: View {
    @StateObject private var engine = AudioEngine()

    var body: some View {
        NavigationStack {
            VStack(spacing: 32) {
                statusCard
                controlButton
                Divider()
                eventList
            }
            .padding()
            .navigationTitle("いびき検知")
        }
    }

    // MARK: - Status card

    private var statusCard: some View {
        VStack(spacing: 12) {
            ZStack {
                Circle()
                    .fill(engine.isSnoring ? Color.red.opacity(0.2) : Color.gray.opacity(0.1))
                    .frame(width: 140, height: 140)
                    .scaleEffect(engine.isSnoring ? 1.1 : 1.0)
                    .animation(.easeInOut(duration: 0.6).repeatForever(autoreverses: true),
                               value: engine.isSnoring)

                Image(systemName: engine.isSnoring ? "waveform.circle.fill" : "moon.zzz.fill")
                    .resizable()
                    .scaledToFit()
                    .frame(width: 64, height: 64)
                    .foregroundColor(engine.isSnoring ? .red : .indigo)
            }

            Text(engine.isSnoring ? "いびき検知中！" : (engine.isMonitoring ? "監視中…" : "停止中"))
                .font(.title2.bold())

            if engine.isMonitoring {
                Text(String(format: "音量: %.1f dB", engine.currentDB))
                    .font(.caption)
                    .foregroundColor(.secondary)
            }
        }
        .padding()
        .frame(maxWidth: .infinity)
        .background(RoundedRectangle(cornerRadius: 20).fill(Color(.systemBackground))
            .shadow(radius: 4))
    }

    // MARK: - Control button

    private var controlButton: some View {
        Button {
            engine.isMonitoring ? engine.stopMonitoring() : engine.startMonitoring()
        } label: {
            Label(engine.isMonitoring ? "監視を停止" : "監視を開始",
                  systemImage: engine.isMonitoring ? "stop.circle.fill" : "play.circle.fill")
                .font(.headline)
                .frame(maxWidth: .infinity)
                .padding()
                .background(engine.isMonitoring ? Color.red : Color.indigo)
                .foregroundColor(.white)
                .clipShape(RoundedRectangle(cornerRadius: 14))
        }
    }

    // MARK: - Event list

    private var eventList: some View {
        Group {
            HStack {
                Text("検知履歴")
                    .font(.headline)
                Spacer()
                Text("\(engine.snoreEvents.count)件")
                    .foregroundColor(.secondary)
            }

            if engine.snoreEvents.isEmpty {
                Text("まだいびきは検知されていません")
                    .foregroundColor(.secondary)
                    .padding(.top)
            } else {
                List(engine.snoreEvents.reversed()) { event in
                    NavigationLink(destination: EventDetailView(event: event)) {
                        VStack(alignment: .leading, spacing: 4) {
                            Text(event.formattedDate)
                                .font(.subheadline.bold())
                            Text(event.fileName)
                                .font(.caption)
                                .foregroundColor(.secondary)
                        }
                    }
                }
                .listStyle(.plain)
                .frame(maxHeight: 300)
            }
        }
    }
}
