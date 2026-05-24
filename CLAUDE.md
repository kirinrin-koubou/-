# プロジェクトメモ

## ゲーム作成時のルール
- ゲーム（HTMLゲーム等）を作成・更新したら、そのファイルを **GoFile** にアップロードし、共有リンク（`downloadPage` のURL）をユーザーに必ず提示する。
- アップロード手順（GoFile 公開API、認証不要）:
  1. `curl -s "https://api.gofile.io/servers"` で利用可能なサーバー名を1つ取得する。
  2. `curl -F "file=@<ファイル名>" "https://<server>.gofile.io/contents/uploadfile"` でアップロードする。
  3. レスポンス JSON の `data.downloadPage`（例: `https://gofile.io/d/xxxxxx`）をリンクとして返す。
