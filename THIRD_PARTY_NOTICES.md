# Üçüncü taraf bileşenler

DucKING aşağıdaki açık kaynak projeleri kullanır veya onlardan uyarlanmıştır.
Tam lisans metinleri `licenses` klasöründedir.

| Bileşen | Kullanım | Lisans | Kaynak |
| --- | --- | --- | --- |
| GoodbyeDPI | `Tools/goodbyedpi/goodbyedpi.exe` | Apache License 2.0, © ValdikSS | https://github.com/ValdikSS/GoodbyeDPI |
| zapret (winws) | `Tools/zapret/winws.exe` | MIT, © bol-van | https://github.com/bol-van/zapret |
| ByeDPI (ciadpi) | `Tools/byedpi/ciadpi.exe` | MIT, © hufrea | https://github.com/hufrea/byedpi |
| WinDivert | `WinDivert.dll`, `WinDivert64.sys` | LGPL v3 (veya GPL v2), © basil00 | https://github.com/basil00/WinDivert |
| ProxiFyre 2.6.1 | `Tools/proxifyre/` (ByeDPI yönlendirmesi) | AGPL-3.0, © NT Kernel Resources | https://github.com/wiresock/proxifyre |
| Windows Packet Filter 3.6.1 | `Tools/prereq/Windows.Packet.Filter.3.6.1.1.x64.msi` (ProxiFyre sürücüsü) | MIT, © Vadim Smirnov | https://github.com/wiresock/ndisapi |
| NLog, Newtonsoft.Json, Topshelf | ProxiFyre bağımlılıkları | BSD-3 / MIT / Apache 2.0 | nlog-project.org, newtonsoft.com, topshelf-project.com |
| Visual C++ 2015-2022 Redistributable | `Tools/prereq/VC_redist.x64.exe` (socksify.dll için) | Microsoft yeniden dağıtım lisansı | https://aka.ms/vs/17/release/vc_redist.x64.exe |
| Cygwin DLL | `Tools/zapret/cygwin1.dll` (winws çalışma zamanı) | LGPL v3 | https://cygwin.com |
| uthash, getline | GoodbyeDPI içinde | BSD | GoodbyeDPI deposu |

ProxiFyre (AGPL-3.0), WinDivert ve cygwin1.dll değiştirilmeden, ayrı dosyalar olarak dağıtılır. Bu
bileşenlerin kaynak kodu yukarıdaki bağlantılardan edinilebilir.

## MIT lisans metni (zapret, ByeDPI)

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
