
@ECHO: Updating %~dp0

@IF NOT EXIST "%GITPATH%" (
 @SET "GITPATH=%PROGRAMFILES%\Git\cmd\"
)

@IF NOT EXIST "%GITPATH%" (
 @SET "GITPATH=%PROGRAMFILES(x86)%\Git\cmd\"
)

@IF EXIST "%GITPATH%" (
 CALL "%GITPATH%git.exe" pull
)