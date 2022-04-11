@echo off
pushd %~dp0
call npm install
call npm run build
call npm run pack
popd
