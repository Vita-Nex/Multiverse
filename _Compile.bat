@SET CURPATH=%~dp0
@SET CSCPATH=%windir%\Microsoft.NET\Framework\v4.0.30319\

@SET OUTPATH=%CURPATH%Build\

@SET SDKPATH=%CURPATH%Multiverse\
@SET SRVPATH=%CURPATH%Multiverse.Server\
@SET CLIPATH=%CURPATH%Multiverse.Client\

@TITLE: Multiverse - http://multiverse.vita-nex.com

::##########


::########## Debug

@DEL "%OUTPATH%Debug\Multiverse.dll"

%CSCPATH%csc.exe /target:library /out:"%OUTPATH%Debug\Multiverse.dll" /recurse:"%SDKPATH%*.cs" /nowarn:0618 /debug /nologo /optimize /unsafe

::##########

@DEL "%OUTPATH%Debug\Multiverse.Server.exe"

%CSCPATH%csc.exe /target:exe /out:"%OUTPATH%Debug\Multiverse.Server.exe" /recurse:"%SRVPATH%*.cs" /r:"%OUTPATH%Debug\Multiverse.dll" /nowarn:0618 /debug /nologo /optimize /unsafe

::##########

@DEL "%OUTPATH%Debug\Multiverse.Client.exe"

%CSCPATH%csc.exe /target:exe /out:"%OUTPATH%Debug\Multiverse.Client.exe" /recurse:"%CLIPATH%*.cs" /r:"%OUTPATH%Debug\Multiverse.dll" /nowarn:0618 /debug /nologo /optimize /unsafe

::##########


::########## Release

@DEL "%OUTPATH%Release\Multiverse.dll"

%CSCPATH%csc.exe /target:library /out:"%OUTPATH%Release\Multiverse.dll" /recurse:"%SDKPATH%*.cs" /nowarn:0618 /nologo /optimize /unsafe

::##########

@DEL "%OUTPATH%Release\Multiverse.Server.exe"

%CSCPATH%csc.exe /target:exe /out:"%OUTPATH%Release\Multiverse.Server.exe" /recurse:"%SRVPATH%*.cs" /r:"%OUTPATH%Release\Multiverse.dll" /nowarn:0618 /nologo /optimize /unsafe

::##########

@DEL "%OUTPATH%Release\Multiverse.Client.exe"

%CSCPATH%csc.exe /target:exe /out:"%OUTPATH%Release\Multiverse.Client.exe" /recurse:"%CLIPATH%*.cs" /r:"%OUTPATH%Release\Multiverse.dll" /nowarn:0618 /nologo /optimize /unsafe

::##########

@ECHO: 

@PAUSE