# TK-Server

SIP and GSM server for Windows Server 2022, with a Windows service runtime and a Windows Forms administration interface.

## Requirements

- Windows, x64. The project targets `net10.0-windows10.0.17763.0`; the Windows platform version in the target framework is not a .NET version.
- .NET 10 SDK to compile. A framework-dependent deployment requires the matching x64 .NET Desktop Runtime.
- Microsoft SQL Server 2022, including the free Express Edition.
- The NuGet packages and separate library projects listed below.
- Device drivers and suitable hardware for GSM features; Windows speech components/voices for speech services.

## Dependency versions

The **DEV project** column is read from the current `TK-Server.csproj` in the developer source repository. The **VS list** column preserves the versions supplied in the original README. Several values differ. This document records that difference; it does not upgrade packages or claim that the newer combination has been compiled or tested. The project file controls package restoration.

Package names use `SqlClient` (lowercase letter l), not `SqIClient` (uppercase letter I).

## Direct NuGet dependencies

Each package link is the official package source on NuGet. Its package page contains upstream project/source links, dependency information and the licence information for that version.

| Package / official source | DEV project | VS list | Purpose |
|---|---|---|---|
| [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/7.0.1) | `7.0.1` | `7.1.1` | SQL Server connections and database access used by the central SQL connection/command classes. |
| [NAudio](https://www.nuget.org/packages/NAudio/2.3.0) | `2.3.0` | `3.3.0` | Audio streams, WAV handling and recording. |
| [SIPSorcery](https://www.nuget.org/packages/SIPSorcery/10.0.6) | `10.0.6` | `10.0.17` | SIP signalling, SDP, RTP and related real-time communication protocols. |
| [SIPSorceryMedia](https://www.nuget.org/packages/SIPSorceryMedia/3.0.1) | `3.0.1` | `3.0.1` | Additional SIPSorcery media integration package retained by the project. |
| [SIPSorceryMedia.Abstractions](https://www.nuget.org/packages/SIPSorceryMedia.Abstractions/10.0.6) | `10.0.6` | `10.0.17` | Shared audio/video format and media endpoint interfaces. |
| [SIPSorceryMedia.Encoders](https://www.nuget.org/packages/SIPSorceryMedia.Encoders/10.0.4) | `10.0.4` | `10.0.4` | Legacy managed/native media encoder integration retained by the project. |
| [SIPSorceryMedia.FFmpeg](https://www.nuget.org/packages/SIPSorceryMedia.FFmpeg/10.0.5) | `10.0.5` | `10.0.17` | FFmpeg media integration; native FFmpeg components must match the selected wrapper version. |
| [SIPSorceryMedia.Windows](https://www.nuget.org/packages/SIPSorceryMedia.Windows/10.0.5) | `10.0.5` | `10.0.17` | Windows-specific media endpoint integration. |
| [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient/4.9.1) | `4.9.1` | `4.9.1` | Additional SQL client package retained in the project references. Current central SQL classes use Microsoft.Data.SqlClient. |
| [System.IO.Ports](https://www.nuget.org/packages/System.IO.Ports/10.0.7) | `10.0.7` | `10.0.14` | Serial/COM-port access for GSM hardware. |
| [System.Management](https://www.nuget.org/packages/System.Management/10.0.7) | `10.0.7` | `10.0.14` | Windows management/WMI access, including device discovery. |
| [System.ServiceProcess.ServiceController](https://www.nuget.org/packages/System.ServiceProcess.ServiceController/10.0.7) | `10.0.7` | `10.0.14` | Windows service control and service-related APIs. |
| [System.Speech](https://www.nuget.org/packages/System.Speech/10.0.7) | `10.0.7` | `10.0.14` | Windows speech synthesis used by voice/time announcement services. |

### Upstream source repositories

| Library family | Source / documentation |
|---|---|
| Microsoft.Data.SqlClient | [dotnet/SqlClient](https://github.com/dotnet/SqlClient) |
| NAudio | [naudio/NAudio](https://github.com/naudio/NAudio) |
| SIPSorcery and current media components | [sipsorcery-org/sipsorcery](https://github.com/sipsorcery-org/sipsorcery), [API documentation](https://sipsorcery-org.github.io/sipsorcery/) |
| Legacy SIPSorceryMedia.Encoders | [sipsorcery/SIPSorceryMedia.Encoders](https://github.com/sipsorcery/SIPSorceryMedia.Encoders) |
| Microsoft System.* packages | Use the **Source repository** and **License** links on the corresponding version-specific NuGet pages above. |

`SIPSorceryMedia.Encoders` 10.0.4 is marked legacy/deprecated on its [official package page](https://www.nuget.org/packages/SIPSorceryMedia.Encoders/10.0.4). It remains a declared dependency of this project; no replacement is made here.

## Separate library projects

These are direct DLL references, not ordinary NuGet dependencies of TK-Server. Restoring TK-Server packages alone does not build or supply these project outputs.

| Library | Source | Required output / role |
|---|---|---|
| `sipcryptlib.dll` | Local `sipcryptlib` project, alongside the DEV TK-Server project. No public repository URL is recorded in the inspected project. | `net8.0` library providing the password encryption/decryption used by TK-Server. |
| `TKFaxEngine.dll` | [fett-tony/TKFaxEngine](https://github.com/fett-tony/TKFaxEngine), clone URL: [TKFaxEngine.git](https://github.com/fett-tony/TKFaxEngine.git) | Separate managed x64 fax library targeting `net10.0-windows10.0.17763.0`, RID `win-x64`. |

Expected references relative to the TK-Server project directory:

```text
..\sipcryptlib\bin\Release\net8.0\sipcryptlib.dll
..\TKFaxEngine\bin\x64\Release\net10.0-windows10.0.17763.0\win-x64\TKFaxEngine.dll
```

The library projects must be available separately and built at the expected paths, or the references must be configured for the local checkout. A sibling repository is not automatically included when cloning TK-Server.

### Dependencies of sipcryptlib

| Package | Version declared by sipcryptlib.csproj | Purpose / source |
|---|---|---|
| [System.Security.Cryptography.ProtectedData](https://www.nuget.org/packages/System.Security.Cryptography.ProtectedData/10.0.3) | `10.0.3` | Windows protected-data APIs; package required by the separate library project. |

### Dependencies of the current managed TKFaxEngine project

The following values are taken from the current local DEV project. For a different GitHub revision, the engine project file in that revision is authoritative.

| Dependency | Declared version / local build | Source and purpose |
|---|---|---|
| [BitMiracle.LibTiff.NET](https://www.nuget.org/packages/BitMiracle.LibTiff.NET/2.4.660) | `2.4.660` | Managed TIFF processing. |
| [libsndfile.NET8](https://www.nuget.org/packages/libsndfile.NET8/1.0.1) | `1.0.1` | Managed sound-file integration; preserve its restored runtime dependencies. |
| `libjpeg.dll` | Local libjpeg-turbo build `3.1.4.1` | [libjpeg-turbo](https://libjpeg-turbo.org/), [upstream source](https://github.com/libjpeg-turbo/libjpeg-turbo). The engine imports TurboJPEG entry points from a DLL named `libjpeg.dll`; the filename alone does not establish ABI compatibility. |

The current engine project explicitly checks for `libjpeg.dll` before building and copies it to the engine output. Its developer-specific `LibJpegNativeDll` path must point to the matching local build. Copying only `TKFaxEngine.dll` is therefore not sufficient for all engine features.

## Additional native source projects in the DEV repository

These projects are present in the developer repository. Their presence does not make every one of them a direct dependency of TK-Server. The project references and the selected engine/media implementation determine which outputs are needed.

| Local source project | Confirmed local information | Upstream / usage |
|---|---|---|
| `libjpeg` | libjpeg-turbo `3.1.4.1`, Windows x64 Release manifest. | [libjpeg-turbo](https://github.com/libjpeg-turbo/libjpeg-turbo); current managed TKFaxEngine requires the compatible `libjpeg.dll`. |
| `libsndfile` | CMake project and x64 Release manifest report `1.2.2`. | [libsndfile/libsndfile](https://github.com/libsndfile/libsndfile); sound-file library. Its native version is separate from the `libsndfile.NET8` wrapper version. |
| `libtiff` | Local native TIFF source/build, documented in its README and build manifest. | [libtiff/libtiff](https://gitlab.com/libtiff/libtiff), [documentation](https://libtiff.gitlab.io/libtiff/). |
| `libsrtp` | CMake project `libsrtp3`, version `3.0.0`. | [cisco/libsrtp](https://github.com/cisco/libsrtp); SRTP implementation. A libsrtp3 build must not be treated as an interchangeable `srtp2.dll`. |
| `srtplib` | Separate local native project linked against OpenSSL crypto and Windows system libraries. | Use this local project and its configured OpenSSL libraries if that implementation is selected; no verified public repository URL is recorded here. |
| `TKFaxEngineFX` | Separate native project linked against `libtiff.lib` and `libjpeg.lib`; copies `libtiff.dll` and `libjpeg.dll` to its output. | Separate from the directly referenced managed `TKFaxEngine.dll`. It is not declared as a direct reference in the inspected TK-Server.csproj. |

### SIPSorcery media runtime files

Some media packages use native codecs and cryptography libraries in addition to managed assemblies. Supply the native files that belong to the exact package/build in use. A newer FFmpeg, OpenSSL or SRTP DLL cannot be assumed compatible solely because its filename is similar.

For FFmpeg, the official upstream is [FFmpeg](https://ffmpeg.org/). Follow the native requirements of the selected [SIPSorceryMedia.FFmpeg](https://www.nuget.org/packages/SIPSorceryMedia.FFmpeg) version. The wrapper package version and the native FFmpeg ABI version are different identifiers.

The TK-Server startup code searches the application sibling `lib` directory and `lib/runtimes` for native dependencies. The project build target also moves managed dependency DLLs into `lib` and copies package runtime files into `lib/runtimes`. Keep the output directory structure intact.

## Restoring and deploying dependencies

1. Make the separate `sipcryptlib` and `TKFaxEngine` projects/outputs available at the configured reference paths.
2. Restore the NuGet packages declared by the project files using Visual Studio. Dependencies brought in by those packages must also be preserved.
3. Build the separate libraries and TK-Server for the configured platform, x64. Native DLLs must use the matching architecture and ABI.
4. Deploy the complete generated application layout, including `bin`, `lib`, native runtime files and configuration files.
5. Use the SQL installation and service scripts in `Wichtig` as documented there.

NuGet dependencies can include further assemblies for logging, caching, authentication, cryptography, audio and Windows interop. Their resolved versions depend on the restored package graph. The inspected Git checkout currently has no `obj/project.assets.json` or build `.deps.json`, so this README does not invent a complete resolved transitive version list. The package pages above provide the declared dependency chains.

## Licences and attribution

Each external dependency keeps its own licence. For NuGet packages, use the licence information for the exact package version linked above. For locally built native libraries, keep the licence files from the source tree and the accompanying build/distribution notices.

The local libjpeg-turbo README points to its `LICENSE.md`; the libsndfile source includes `COPYING`; libsrtp includes `LICENSE`. libtiff and native codec/cryptography dependencies must retain their own applicable notices. Licensing for a wrapper does not by itself describe every native component it loads.

The source and package links in this section identify the dependencies; they do not change their licences or the TK-Server licence.

Created with assistance from OpenAI.
