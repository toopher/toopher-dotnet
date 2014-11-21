# ToopherAPI DotNet Client

#### .NET Framework Version
>=4.5

#### C# Version
>=5.0

#### Installing Dependencies
Toopher uses [Mono](http://www.mono-project.com/).

To install Mono with Homebrew run:
```shell
$ brew install mono
```

#### Tests
To run the tests enter:
```shell
$ xbuild
$ nunit-console ./ToopherDotNetTests/bin/Debug/ToopherDotNetTests.dll -exclude Integration,NotWorkingOnMono
```
