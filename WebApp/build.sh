#!/bin/bash
cp ../../../VisemeDemoPortal/Pages/VideoReceiverPage.html ./client/public/receiver/index.html
npm install
npm run build
npm run pack
