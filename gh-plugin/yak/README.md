## 公式マニュアル
https://developer.rhino3d.com/guides/yak/

## Publish
.csprojのversionを上げてから以下のコマンドを実行(manifest.ymlのバージョンは自動で更新されるはず)
```
./publish.sh
```

## Login
※ Windowsだと"&"を先頭につける必要あり
```
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" login
```

## Search
```
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" search  --all --prerelease GHCodeSync
```

## Remove
```
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" yank GHCodeSync <version>
```