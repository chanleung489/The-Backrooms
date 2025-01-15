echo building
stat -c '%y' ~/Documents/rw/steamdir/RainWorld_Data/StreamingAssets/mods/znery.backrooms/plugins/znery.backrooms.dll
dotnet build -c Debug
cp bin/Debug/net48/* ../znery.backrooms/plugins
cp -ruv ../znery.backrooms/ ~/Documents/rw/steamdir/RainWorld_Data/StreamingAssets/mods/znery.backrooms/ > cp.log
stat -c '%y' ~/Documents/rw/steamdir/RainWorld_Data/StreamingAssets/mods/znery.backrooms/plugins/znery.backrooms.dll 
