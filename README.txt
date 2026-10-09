ClearSpace 1.4.3 - Windows Cache Cleaner

Made by Rahul Kakkireni

Install ClearSpace-Setup-1.4.3.msi to upgrade an earlier version, or run ClearSpace.exe.

1. Click Run as administrator for Windows Temp and Windows Update downloads.
   Approve the Windows UAC prompt using the same Windows account.
2. Chrome, Edge, Brave, Vivaldi, Chromium, Yandex, Opera, Opera GX and Firefox
   cache can be scanned and cleaned while browsers are open.
   Files currently in use are safely skipped; close the browser to clean those too.
3. Choose 24 hours, 7 days, 30 days, or Any age, then click Scan PC.
   Any age includes recent files. Locked, linked and changed files remain protected.
4. Select locations, review Preview selected files, then click Clean selected.
   Windows Update downloads are not selected automatically.
5. Use Paths & skipped files for actual folder paths and detailed skip reasons.

Supported locations:
- The installing user's LocalAppData Temp folder and configured LocalAppData
  Temp/Tmp folder, discovered dynamically from Windows. Windows 8.3 aliases are
  normalized to the current user's full path; no specific user name is built in.
- Windows\Temp.
- Chromium-family profile Cache (including Cache_Data), Code Cache, GPUCache,
  Dawn, media, network, service-worker script and CacheStorage folders, plus
  shared shader/component caches. Chrome, Edge, Brave, Vivaldi, Chromium,
  Yandex, Opera and Opera GX locations are discovered dynamically.
- Firefox cache2, startup, shader and thumbnail cache folders across profiles.
- Windows web, thumbnail and DirectX shader caches, plus packaged-app TempState
  and INetCache folders. Packaged-app LocalCache is excluded because apps may
  keep durable account or working data there despite the folder name.
- Windows\SoftwareDistribution\Download only.

Windows Update cleanup:
Administrator access is required. The confirmation explains that BITS and Windows
Update services will be stopped temporarily. Only services stopped by ClearSpace
are restarted afterward, including after cancellation or a handled failure.
Cleanup is skipped during active Windows servicing or a pending update restart.
If services restart during cleaning, remaining update files are skipped.
Restart failures appear in the results and require attention.
Update packages may need downloading again. Update history, DataStore, WinSxS,
installed updates, delivery optimization storage, and catroot2 are excluded.

Documents, photos, user Downloads, cookies, passwords, browsing history,
Recycle Bin, registry cleaning and Prefetch are not cleaning targets.
No directories are deleted. Some empty cache folders may remain.
Files changed since scanning, hardlinks, symbolic links, junctions, locked files,
and inaccessible files are skipped. Reported space is the size of deleted files;
actual free disk space can differ with compression or concurrent disk activity.
Deletion is permanent. Cancellation does not undo completed deletions.

Windows 10/11 with .NET Framework 4.5 or later. This local build is unsigned.
The app operates locally and does not upload scan results.

Build with build.ps1, which uses Windows' .NET Framework C# compiler and references
System.Windows.Forms, System.Drawing, System.Core and System.ServiceProcess.
Frontend.cs contains the interface; CacheCleaner.cs contains the engine and tests.

Design reference:
https://github.com/emilkowalski/skills/blob/main/skills/apple-design/SKILL.md
Windows Update service sequencing reference:
https://learn.microsoft.com/en-us/troubleshoot/windows-client/installing-updates-features-roles/additional-resources-for-windows-update
