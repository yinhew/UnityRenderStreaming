#!/bin/bash
cp ../../../VisemeDemoUnity/Assets/Resources/VideoReceiverPage.html ./client/public/receiver/index.html
npm install
npm run build
npm run pack
