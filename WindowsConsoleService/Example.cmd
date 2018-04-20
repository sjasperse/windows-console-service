
@echo off
:loop

echo Hello world

ping localhost -n 2 > nul
goto loop