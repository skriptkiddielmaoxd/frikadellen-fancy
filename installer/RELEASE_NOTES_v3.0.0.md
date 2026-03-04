## Frikadellen BAF v3.0.0

Release artifacts:

- Installer: `FrikadellenBAF_Setup_v3.0.0.exe` (in this repo under `installer/output`)
- SHA256: `5C7C59BA32EB4E410AE17D36355D48C76DCA8948390D82CA78C30E73224D3909`

Changes in this release:

- Built Windows installer bundle (Inno Setup) containing the Avalonia UI and Rust backend.
- Suppressed noisy Azalea entity/packet logs by adjusting the app logging filter.
- Added `recover_clone/` to `.gitignore` to avoid accidental commits of local recovery data.

Notes and verification

- To verify the installer on your machine, compute SHA256 and compare with the value above:

```powershell
Get-FileHash .\installer\output\FrikadellenBAF_Setup_v3.0.0.exe -Algorithm SHA256
```

- If you want me to create the GitHub release and upload the installer, ensure the `gh` CLI is authenticated in this environment; I will attempt that automatically.

Enjoy!
