ClearSpace - Windows Cache Cleaner

Open ClearSpace.exe. No installer, network connection or administrator access is needed.
1. Choose a minimum file age (24 hours, 7 days, or 30 days).
2. Click Scan PC. Scanning does not delete anything.
3. Select the locations to clean and use Preview files to inspect them.
4. Click Clean selected and confirm the permanent deletion.

Locations: your LocalAppData Temp folder, accessible Windows Temp files,
and Chrome/Edge Default and Profile folders' Cache_Data, Code Cache and GPUCache.
Close Chrome/Edge completely before scanning their cache. Background browser
processes also cause that browser category to be skipped.

Files created or changed recently, junctions/symbolic links, files modified since
the scan, locked files and inaccessible files are skipped. No folders are deleted.
Downloads, Documents, photos, Recycle Bin, cookies, passwords, browsing history,
registry, Windows Update data and Prefetch are not cleaning targets.

Deleted files cannot be restored through this application. Apps may rebuild
their caches, and the next load may be slower. Reported space is file size;
actual free disk space can differ with compression or concurrent disk activity.
Cancellation stops after the current file. It does not undo completed deletions.

Portable application for Windows with .NET Framework 4.x (built into Windows 10/11).
This local build is unsigned. Keep the entire folder if you want the source.

Build source using Windows' included .NET Framework compiler:
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:ClearSpace.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll CacheCleaner.cs Frontend.cs

UI design adapted from the apple-design skill:
https://github.com/emilkowalski/skills/blob/main/skills/apple-design/SKILL.md
Uses Windows system fonts, pale surfaces, rounded controls, immediate press feedback,
keyboard focus cues, and native high-contrast colors. No decorative motion is used.
Frontend.cs contains the interface; CacheCleaner.cs contains the cleaning engine.
