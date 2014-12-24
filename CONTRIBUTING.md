# ToopherAPI DotNet Client

#### .NET Framework Version
>=4.5

#### C# Version
>=5.0

#### Installing Dependencies
This library was developed on Windows and OS X. To run .NET on OS X, we use [Mono](http://www.mono-project.com/).

To install Mono with Homebrew run:
```shell
$ brew install mono
```

#### Tests
To test, run:
```shell
$ xbuild
$ nunit-console ./ToopherDotNetTests/bin/Debug/ToopherDotNetTests.dll -exclude Integration,NotWorkingOnMono
```
