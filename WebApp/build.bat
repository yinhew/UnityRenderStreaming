@echo off
pushd %~dp0
copy ..\..\..\AvatarHost\Pages\VideoReceiverPage.html .\client\public\receiver\index.html
call npm install
call npm run build
call npm run pack
popd
