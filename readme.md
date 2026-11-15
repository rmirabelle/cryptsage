# Crypt Sage

A modern, minimal Windows front-end for [VeraCrypt](https://www.veracrypt.fr/). Aims to be the simplest possible replacement for the now-defunct TrueCrypt: one big button to mount an encrypted volume, and nothing else in the way.

## What it does

- **Mount** an existing encrypted volume by browsing to the file, entering the password, and letting Crypt Sage auto-assign the first free drive letter (Z → D).
- **Create** a new AES / SHA-512 / ExFAT volume in fixed sizes from 1 MB to 10 GB, then mount it immediately.
- **Dismount all** with one click. Crypt Sage also dismounts every volume it knows about at startup, and warns if you try to close the app while volumes it mounted are still live.

By design, Crypt Sage **never** keeps a recent-files list, history, or any other persistent record of what you mount or where it lives. Privacy by omission.

## How it works

Crypt Sage is a thin .NET 10 / WPF UI on top of VeraCrypt's command-line binaries. The installer ships VeraCrypt's portable distribution (`VeraCrypt-x64.exe`, `VeraCrypt Format-x64.exe`, and the kernel driver) into the app's install folder. VeraCrypt's portable mode loads the driver on demand at mount time and unloads it when the app closes — no separate VeraCrypt installation is required on the target machine.

The core launcher lives in [CryptSage.Core](src/CryptSage.Core/VeraCryptLauncher.cs); it resolves the bundled binaries relative to the running `.exe`, then shells out with `/q /s` (silent, no UI) for mount, dismount, and create operations.

## Project layout

```
src/
  CryptSage.App/      WPF UI (WPF-UI / Fluent), .NET 10
  CryptSage.Core/     VeraCrypt CLI wrapper + drive-letter helper
installer/
  CryptSage.iss       Inno Setup 6 script
  vendor/VeraCrypt/   Bundled VeraCrypt portable binaries + driver
tools/
  IconGen/            One-off app/mount icon generator
```

## Building

Requirements:
- .NET 10 SDK
- Windows 10 (build 10240) or later, x64
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (only for building the installer)

Build and run the app:

```powershell
dotnet build CryptSage.slnx
dotnet run --project src/CryptSage.App
```

Publish a single-file release and package the installer:

```powershell
dotnet publish src/CryptSage.App `
  -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true `
  -o publish

& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\CryptSage.iss
# -> installer\Output\CryptSage-Setup.exe
```

The installer requires admin (UAC) because mounting volumes loads VeraCrypt's kernel driver.

## License

Crypt Sage's own source is unlicensed at this stage (TBD). The bundled VeraCrypt binaries are distributed under the Apache License 2.0; their `LICENSE` and `NOTICE` are shipped alongside the binaries in `installer/vendor/VeraCrypt/` and installed to the app folder.
