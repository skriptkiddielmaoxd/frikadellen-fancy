Frikadellen BAF installer

This folder contains an Inno Setup script and a PowerShell helper to produce a single Windows installer
that bundles the Rust backend and the Avalonia UI and creates a desktop shortcut.

Quick steps

1. Build releases already done in this repo root:

   - Rust backend: `cargo build --release` (produces binaries in `target/release`)
   - UI: `dotnet publish Frikadellen.UI/Frikadellen.UI.csproj -c Release -o Frikadellen.UI/publish`

2. Run the packaging script from an elevated PowerShell prompt (from repo root):

```powershell
.
\installer\build-installer.ps1
```

3. If Inno Setup is installed, the script will call `ISCC.exe` and place the installer in `installer/output`.
   If Inno Setup isn't installed, the script will leave a staging folder at `installer/staging/app` which
   you can compile manually using Inno Setup Compiler (ISCC.exe):

```powershell
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "installer\frikadellen_installer.iss"
```

Notes

- The installer creates entries in Program Files and a desktop shortcut to `Frikadellen.UI.exe`.
- By default the script copies all `.exe` files from `target/release` into the installer; if you need
  additional runtime files (DLLs, data files) add them to the staging logic in `build-installer.ps1`.
- The Inno Setup script expects the staging folder at `installer/staging/app`.

Customization

- Edit `installer/frikadellen_installer.iss` to change AppName, version, icons, or licensing.
- Add code to the `build-installer.ps1` script if you'd like to include additional files or installers
  (e.g. Redistributable installers).
