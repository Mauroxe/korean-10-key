@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe nao encontrado. Precisa do .NET Framework 4.x ^(vem com o Windows 10/11^).
  pause
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:Teclado10Key.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll Program.cs Data.cs
if errorlevel 1 (
  echo.
  echo Erro na compilacao.
  pause
  exit /b 1
)
echo.
echo OK: Teclado10Key.exe criado. Podes copiar so esse ficheiro para onde quiseres.
pause
