# Third-party notices

> **Türkçe:** DucKING aşağıdaki açık kaynak bileşenleri kullanır. Hepsi değiştirilmeden, ayrı dosyalar
> olarak dağıtılır; tam lisans metinleri `licenses` klasöründedir.
>
> **Русский:** DucKING использует перечисленные ниже компоненты с открытым исходным кодом. Все они
> распространяются без изменений, отдельными файлами; полные тексты лицензий находятся в папке `licenses`.

DucKING itself is licensed under the Apache License 2.0 (see [LICENSE](LICENSE) and [NOTICE](NOTICE)).
It bundles the following third-party components. All of them are distributed **unmodified**, as separate
files, under their own licenses. The full license texts are in the [`licenses`](licenses) folder, which
is also installed next to `DucKING.exe`.

| Component | Version | Bundled as | License | Source |
| --- | --- | --- | --- | --- |
| GoodbyeDPI | 0.2.3rc3 | `Tools/goodbyedpi/goodbyedpi.exe` | Apache License 2.0, © ValdikSS | [release 0.2.3rc3](https://github.com/ValdikSS/GoodbyeDPI/releases/tag/0.2.3rc3) |
| zapret (winws) | v72.13 | `Tools/zapret/winws.exe` | MIT, © bol-van | [release v72.13](https://github.com/bol-van/zapret/releases/tag/v72.13) |
| ByeDPI (ciadpi) | 0.17.3 | `Tools/byedpi/ciadpi.exe` | MIT, © hufrea | [release v0.17.3](https://github.com/hufrea/byedpi/releases/tag/v0.17.3) |
| WinDivert | 2.2 | `WinDivert.dll`, `WinDivert64.sys` (with GoodbyeDPI and with zapret) | LGPL v3 or GPL v2, © basil00 | [2.2.x releases, latest v2.2.2](https://github.com/basil00/WinDivert/releases/tag/v2.2.2) |
| Cygwin DLL | 3.4.10 | `Tools/zapret/cygwin1.dll` (winws runtime) | LGPL v3, © Red Hat and contributors | `https://cygwin.com/git/newlib-cygwin.git`, tag `cygwin-3.4.10` ([licensing](https://cygwin.com/licensing.html)) |
| ProxiFyre | 2.6.1 | `Tools/proxifyre/` (routes Discord and Roblox to ByeDPI) | AGPL-3.0, © NT Kernel Resources | [release v2.6.1](https://github.com/wiresock/proxifyre/releases/tag/v2.6.1) |
| Windows Packet Filter | 3.6.1 | `Tools/prereq/Windows.Packet.Filter.3.6.1.1.x64.msi` (ProxiFyre driver) | MIT, © Vadim Smirnov | [release v3.6.1](https://github.com/wiresock/ndisapi/releases/tag/v3.6.1) |
| NLog | 5.2.3 | `Tools/proxifyre/NLog.dll` (ProxiFyre dependency) | BSD 3-Clause | https://github.com/NLog/NLog |
| Newtonsoft.Json | 13.0.3 | `Tools/proxifyre/Newtonsoft.Json.dll` (ProxiFyre dependency) | MIT | https://github.com/JamesNK/Newtonsoft.Json |
| Topshelf | 4.3.0 | `Tools/proxifyre/Topshelf.dll` (ProxiFyre dependency) | Apache License 2.0 | https://github.com/Topshelf/Topshelf |
| uthash, getline | — | compiled into GoodbyeDPI | BSD | GoodbyeDPI repository |
| Visual C++ 2015-2022 Redistributable | 14.44.35211 | `Tools/prereq/VC_redist.x64.exe` (needed by `socksify.dll`) | Microsoft redistributable license | https://aka.ms/vs/17/release/vc_redist.x64.exe |

## Copyleft components

ProxiFyre (AGPL-3.0), WinDivert (LGPL v3 / GPL v2) and the Cygwin DLL (LGPL v3) are shipped as
separate, unmodified programs and libraries. DucKING starts them as separate processes or services and
does not link to them. The complete corresponding source code for the exact versions above is
available at the links in the table. If any of these links stops working, open an
[issue](https://github.com/uy4r/DucKING/issues) and the source will be provided.

## License files

| File | Component |
| --- | --- |
| `licenses/LICENSE-goodbyedpi.txt` | GoodbyeDPI (Apache 2.0) |
| `licenses/LICENSE-zapret.txt` | zapret (MIT) |
| `licenses/LICENSE-byedpi.txt` | ByeDPI (MIT) |
| `licenses/LICENSE-windivert.txt` | WinDivert (LGPL v3 / GPL v2) |
| `licenses/LICENSE-cygwin.txt` | Cygwin DLL (LGPL v3 and GPL v3 texts) |
| `licenses/LICENSE-ProxiFyre.txt` | ProxiFyre (AGPL-3.0) |
| `licenses/LICENSE-WindowsPacketFilter-ndisapi.txt` | Windows Packet Filter (MIT) |
| `licenses/LICENSE-NLog.txt` | NLog (BSD 3-Clause) |
| `licenses/LICENSE-Newtonsoft.Json.txt` | Newtonsoft.Json (MIT) |
| `licenses/LICENSE-Topshelf.txt` | Topshelf (Apache 2.0) |
| `licenses/LICENSE-uthash.txt`, `licenses/LICENSE-getline.txt` | uthash, getline (BSD) |
