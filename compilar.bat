@echo off
rem Compila o FRAMEUP.exe com o compilador C# que ja vem no Windows (.NET Framework 4.8).
rem Uso: compilar.bat            -> gera ..\FRAMEUP.exe
rem      compilar.bat captura    -> gera a versao que tira prints para o anuncio
setlocal
cd /d "%~dp0"
set "NET=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "REFS=/r:"%NET%\WPF\PresentationFramework.dll" /r:"%NET%\WPF\PresentationCore.dll" /r:"%NET%\WPF\WindowsBase.dll" /r:"%NET%\System.Xaml.dll" /r:System.Management.dll /r:System.Web.Extensions.dll"

if /i "%~1"=="captura" (
    "%NET%\csc.exe" /nologo /target:winexe /optimize+ /define:CAPTURA /out:..\captura.exe /win32icon:icone.ico /resource:Janela.xaml,FRAMEUP.Janela.xaml %REFS% *.cs
) else (
    "%NET%\csc.exe" /nologo /target:winexe /optimize+ /out:..\FRAMEUP.exe /win32icon:icone.ico /win32manifest:app.manifest /resource:Janela.xaml,FRAMEUP.Janela.xaml %REFS% *.cs
)
