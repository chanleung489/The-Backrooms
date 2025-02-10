echo "===== Build - $(date) =====" | tee -a build.log
stat -c '%y' ~/Documents/rw/localmods/znery.backrooms/plugins/znery.backrooms.dll
dotnet build src/ -c Debug | tee -a build.log
cp src/bin/Debug/net48/* znery.backrooms/plugins
cp -ruv znery.backrooms/ ~/Documents/rw/localmods/ >> build.log
stat -c '%y' ~/Documents/rw/localmods/znery.backrooms/plugins/znery.backrooms.dll | tee -a build.log
echo "" | tee -a build.log
