# depo

Dependency manager and build system for C/C++ on top of ninja and clang.

Features:
- Supports Windows and macOS. Note: for Windows it is required to have clang installed via Visual Studio Installer.
- Integrates with any editor with support of clangd with `compile_commands.json` file. It will be created/updated automatically on build stage.
- Clones dependencies recursively with git, svn, https tar.xz archive.
- For tar.xz archive dependencies supports timestamps, so it will not be pulled again if no changes.
- Very fast startup. On Windows when no file has changed, build will run in 80 milliseconds.
- When project links with another project, it automatically pulls public flags, link libraries, include dirs (similar to CMake behavior).

## Build

```bash
dotnet publish -c Release
```

In `C:/bin` or `~/.local/bin` (in $PATH) add file:

`depo.bat`:

```batch
@echo off
D:\src\depo\bin\Release\net10.0\win-x64\publish\depo.exe %*
```

`depo`:

```shell
/d/src/depo/bin/Release/net10.0/win-x64/publish/depo.exe $@
```

## Usage

In directory create `depo.lisp` file. See [Examples](#Examples) section for more details about its content.

Run:

- `depo` - it will pull deps, clean working directory, build solution.
- `depo build` - build solution (default debug config).
- `depo build -r` - build solution in release config.
- `depo run` - build solution and run first workspace target.
- `depo run amogus` - build solution and run target named `amogus`.
- `depo run amogus some arg` - build solution and run target named `amogus` with arguments `some arg`.
- `depo clean` - remove build files.
- `depo vscode` - write some vscode settings files.
- `depo pull` - initialize/update dependencies to actual versions.

## Todo:

- proto generator
- automation script for installing depo
- recursive deps

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

(bin 'win lib/dxtex.dll)  ;; this file will be auto copied to bin dir
```

> Also note, paths are always relative to current `depo.lisp` file.

Top level project definition (executable):

```lisp
;; these dependencies will be cloned to %workspace%/deps folder
(deps
  (git cc git@github.com:VadikPtr/rebus.git)
  (archive sdl3 https://some-binary-server.ru/sdl3-{os}.tar.xz)
  (archive dxtex https://some-binary-server.ru/dxtex-{os}.tar.xz)
  (archive fontbake https://some-binary-server.ru/fontbake-{os}.tar.xz)
  (archive nvtt https://some-binary-server.ru/nvtt-{os}.tar.xz)
  (archive gns https://some-binary-server.ru/gns-{os}.tar.xz)
  (svn lalia-data svn://some-svn-repo.ru/lalia-data)
)

;; similar to add_subdirectory in CMake
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
  (link 'prj cc clay dxtex fontbake gns nvtt sdl3 gapi)
)

(targets lalia)
```
