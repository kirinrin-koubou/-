import Foundation

struct SnoreEvent: Identifiable {
    let id = UUID()
    let date: Date
    let fileURL: URL

    var formattedDate: String {
        let f = DateFormatter()
        f.dateStyle = .short
        f.timeStyle = .medium
        return f.string(from: date)
    }

    var fileName: String { fileURL.lastPathComponent }

    /// Duration of the recorded clip in seconds
    var duration: TimeInterval? {
        let asset = try? AVURLAsset(url: fileURL)
        return asset?.duration.seconds
    }
}

import AVFoundation
