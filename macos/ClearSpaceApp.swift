import SwiftUI
import AppKit

struct CacheFile: Identifiable {
    let id = UUID()
    let url: URL
    let root: URL
    let size: Int64
    let modified: Date
    let created: Date
}

struct CacheCategory: Identifiable {
    let id = UUID()
    let name: String
    let detail: String
    let roots: [URL]
    var selected = true
    var files: [CacheFile] = []
    var recent = 0
    var skipped = 0
    var bytes: Int64 { files.reduce(0) { $0 + $1.size } }
}

enum AgeChoice: String, CaseIterable, Identifiable {
    case day = "24 hours", week = "7 days", month = "30 days", any = "Any age"
    var id: String { rawValue }
    var cutoff: Date {
        switch self {
        case .day: return Date().addingTimeInterval(-86400)
        case .week: return Date().addingTimeInterval(-604800)
        case .month: return Date().addingTimeInterval(-2592000)
        case .any: return .distantFuture
        }
    }
}

final class CleanerModel: ObservableObject {
    @Published var categories: [CacheCategory] = CleanerModel.makeCategories()
    @Published var age: AgeChoice = .day
    @Published var busy = false
    @Published var scanned = false
    @Published var status = "Ready to scan. Your personal files are excluded."
    @Published var progress = 0.0
    @Published var showConfirmation = false

    static let keys: Set<URLResourceKey> = [.isDirectoryKey, .isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey, .contentModificationDateKey, .creationDateKey]

    static func makeCategories() -> [CacheCategory] {
        let fm = FileManager.default
        let home = fm.homeDirectoryForCurrentUser
        let caches = home.appendingPathComponent("Library/Caches", isDirectory: true)
        let browserRoots = [
            caches.appendingPathComponent("Google/Chrome", isDirectory: true),
            caches.appendingPathComponent("Microsoft Edge", isDirectory: true),
            caches.appendingPathComponent("BraveSoftware/Brave-Browser", isDirectory: true),
            caches.appendingPathComponent("Firefox/Profiles", isDirectory: true),
            caches.appendingPathComponent("com.apple.Safari", isDirectory: true),
            caches.appendingPathComponent("com.operasoftware.Opera", isDirectory: true),
            caches.appendingPathComponent("Vivaldi", isDirectory: true)
        ]
        let excluded = Set(["Google", "Microsoft Edge", "BraveSoftware", "Firefox", "com.apple.Safari", "com.operasoftware.Opera", "Vivaldi"])
        let otherCaches = (try? fm.contentsOfDirectory(at: caches, includingPropertiesForKeys: [.isDirectoryKey, .isSymbolicLinkKey], options: [.skipsHiddenFiles]))?
            .filter { !excluded.contains($0.lastPathComponent) } ?? []
        return [
            CacheCategory(name: "User temporary files", detail: "The current Mac user's dynamic temporary folder", roots: [URL(fileURLWithPath: NSTemporaryDirectory(), isDirectory: true)]),
            CacheCategory(name: "System temporary files", detail: "Accessible files in /private/tmp", roots: [URL(fileURLWithPath: "/private/tmp", isDirectory: true)]),
            CacheCategory(name: "Browser caches", detail: "Chrome, Edge, Brave, Firefox, Safari, Opera and Vivaldi caches", roots: browserRoots),
            CacheCategory(name: "Application caches", detail: "Other folders inside ~/Library/Caches", roots: otherCaches)
        ]
    }

    static func format(_ bytes: Int64) -> String {
        ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file)
    }

    private static func inside(_ file: URL, root: URL) -> Bool {
        let path = file.standardizedFileURL.path
        var base = root.standardizedFileURL.path
        while base.count > 1 && base.hasSuffix("/") { base.removeLast() }
        return path.hasPrefix(base + "/")
    }

    private static func scanCategory(_ category: CacheCategory, cutoff: Date) -> CacheCategory {
        var result = category
        result.files = []
        result.recent = 0
        result.skipped = 0
        var seen = Set<String>()
        let fm = FileManager.default
        for root in category.roots {
            guard let rootValues = try? root.resourceValues(forKeys: [.isDirectoryKey, .isSymbolicLinkKey]), rootValues.isDirectory == true, rootValues.isSymbolicLink != true else { continue }
            guard let iterator = fm.enumerator(at: root, includingPropertiesForKeys: Array(keys), options: [], errorHandler: { _, _ in result.skipped += 1; return true }) else { continue }
            while let item = iterator.nextObject() as? URL {
                guard let values = try? item.resourceValues(forKeys: keys) else { result.skipped += 1; continue }
                if values.isSymbolicLink == true {
                    if values.isDirectory == true { iterator.skipDescendants() }
                    result.skipped += 1
                    continue
                }
                guard values.isRegularFile == true, inside(item, root: root) else { continue }
                let path = item.standardizedFileURL.path
                guard seen.insert(path).inserted else { continue }
                let modified = values.contentModificationDate ?? .distantPast
                let created = values.creationDate ?? .distantPast
                if modified >= cutoff || created >= cutoff { result.recent += 1; continue }
                result.files.append(CacheFile(url: item, root: root, size: Int64(values.fileSize ?? 0), modified: modified, created: created))
            }
        }
        return result
    }

    func scan() {
        guard !busy else { return }
        busy = true; scanned = false; progress = 0; status = "Scanning cache folders…"
        let snapshot = categories
        let cutoff = age.cutoff
        DispatchQueue.global(qos: .userInitiated).async {
            var updated: [CacheCategory] = []
            for (index, category) in snapshot.enumerated() {
                updated.append(Self.scanCategory(category, cutoff: cutoff))
                DispatchQueue.main.async { self.progress = Double(index + 1) / Double(snapshot.count) }
            }
            DispatchQueue.main.async {
                self.categories = updated
                self.busy = false; self.scanned = true
                self.status = "Scan complete. Review the selected locations before cleaning."
            }
        }
    }

    func clean() {
        guard scanned, !busy else { return }
        busy = true; status = "Removing selected cache files…"
        let selected = categories.filter { $0.selected }.flatMap { $0.files }
        let cutoff = age.cutoff
        DispatchQueue.global(qos: .userInitiated).async {
            var removed = 0, skipped = 0
            var bytes: Int64 = 0
            let fm = FileManager.default
            for entry in selected {
                guard Self.inside(entry.url, root: entry.root), let values = try? entry.url.resourceValues(forKeys: Self.keys), values.isRegularFile == true, values.isSymbolicLink != true else { skipped += 1; continue }
                let modified = values.contentModificationDate ?? .distantPast
                let created = values.creationDate ?? .distantPast
                guard Int64(values.fileSize ?? 0) == entry.size, modified == entry.modified, created == entry.created, modified < cutoff, created < cutoff else { skipped += 1; continue }
                do { try fm.removeItem(at: entry.url); removed += 1; bytes += entry.size } catch { skipped += 1 }
            }
            DispatchQueue.main.async {
                self.busy = false; self.scanned = false
                self.categories = Self.makeCategories()
                self.status = "Removed \(removed) files (\(Self.format(bytes))); skipped \(skipped). Scan again to refresh."
            }
        }
    }

    var selectedFiles: Int { categories.filter { $0.selected }.reduce(0) { $0 + $1.files.count } }
    var selectedBytes: Int64 { categories.filter { $0.selected }.reduce(0) { $0 + $1.bytes } }
}

struct ContentView: View {
    @StateObject private var cleaner = CleanerModel()

    var body: some View {
        VStack(spacing: 18) {
            HStack {
                Image(systemName: "sparkles").font(.system(size: 28)).foregroundColor(.blue)
                VStack(alignment: .leading, spacing: 1) {
                    Text("ClearSpace").font(.title.bold())
                    Text("macOS cache cleaner  •  Made by Rahul Kakkireni").font(.caption).foregroundColor(.secondary)
                }
                Spacer()
                VStack(alignment: .trailing) {
                    Text(CleanerModel.format(cleaner.selectedBytes)).font(.title2.bold())
                    Text("\(cleaner.selectedFiles) selected files").font(.caption).foregroundColor(.secondary)
                }
            }

            Picker("File age", selection: $cleaner.age) {
                ForEach(AgeChoice.allCases) { choice in Text(choice.rawValue).tag(choice) }
            }.pickerStyle(.segmented).disabled(cleaner.busy)

            List {
                ForEach($cleaner.categories) { $category in
                    Toggle(isOn: $category.selected) {
                        HStack {
                            VStack(alignment: .leading) {
                                Text(category.name).font(.headline)
                                Text(category.detail).font(.caption).foregroundColor(.secondary)
                            }
                            Spacer()
                            VStack(alignment: .trailing) {
                                Text(CleanerModel.format(category.bytes)).bold()
                                Text("\(category.files.count) files").font(.caption).foregroundColor(.secondary)
                            }
                        }.padding(.vertical, 5)
                    }.disabled(cleaner.busy)
                }
            }.frame(minHeight: 280)

            if cleaner.busy { ProgressView(value: cleaner.progress) }
            HStack {
                Text(cleaner.status).font(.callout).foregroundColor(.secondary)
                Spacer()
                Button("Scan Mac") { cleaner.scan() }.keyboardShortcut("r").disabled(cleaner.busy)
                Button("Clean selected") { cleaner.showConfirmation = true }
                    .keyboardShortcut(.defaultAction)
                    .disabled(cleaner.busy || !cleaner.scanned || cleaner.selectedFiles == 0)
            }
        }
        .padding(24)
        .frame(minWidth: 760, minHeight: 570)
        .alert("Permanently delete selected cache files?", isPresented: $cleaner.showConfirmation) {
            Button("Cancel", role: .cancel) { }
            Button("Delete cache", role: .destructive) { cleaner.clean() }
        } message: {
            Text("\(cleaner.selectedFiles) files (\(CleanerModel.format(cleaner.selectedBytes))) will be removed. Locked, changed, linked and inaccessible files are skipped.")
        }
    }
}

@main
struct ClearSpaceApp: App {
    var body: some Scene {
        WindowGroup { ContentView() }
            .windowStyle(.titleBar)
    }
}
