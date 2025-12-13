# depo

Dependency manager and meta-build system for C/C++ on top of ninja

## Build

```bash
dotnet publish -c Release
```

In `C:/bin` or `~/.local/bin` (in $PATH) add file:

depo.bat

```batch
@echo off
D:\src\depo\bin\Release\net10.0\win-x64\publish\depo.exe %*
```

depo

```shell
/d/src/depo/bin/Release/net10.0/win-x64/publish/depo.exe $@
```

Soon I will add automatic installation script.

## Examples

DLL library with include folder `inc` and link to static library project `glad`:

```lisp
(project gapi
  (kind dll)
  (files *.cpp)
  (include 'pub inc)
  (link 'prj glad)
  (flags 'iface -DmGAPIWrapperGLImportInterface)
  (flags -DmGAPIWrapperGLExportInterface)
)
```

Pre-built DLL interface library with auto copy DLL to final binary directory:

```lisp
(project dxtex
  (kind iface)
  (include 'iface include)
  (link 'iface 'win lib/dxtex.lib)
)

(bin 'win lib/dxtex.dll)
```

Top level project definition (executable):

```lisp
(deps
  (git cc git@github.com:VadikPtr/rebus.git)
  (archive sdl3 https://some-binary-server.ru/sdl3-{os}.tar.xz)
  (archive dxtex https://some-binary-server.ru/dxtex-{os}.tar.xz)
  (archive fontbake https://some-binary-server.ru/fontbake-{os}.tar.xz)
  (archive nvtt https://some-binary-server.ru/nvtt-{os}.tar.xz)
  (archive gns https://some-binary-server.ru/gns-{os}.tar.xz)
  (svn lalia-data svn://some-svn-repo.ru/lalia-data)
)

(require
  deps/cc
  deps/clay
  deps/dxtex
  deps/fontbake
  deps/glad
  deps/gns
  deps/nvtt
  deps/sdl3
  gapi
)

(project lalia
  (kind exe)
  (files lalia/*.cpp)
  (include lalia)
  (link 'prj
    cc
    clay
    dxtex
    fontbake
    gns
    nvtt
    sdl3
    gapi)
)

(targets lalia)
```
