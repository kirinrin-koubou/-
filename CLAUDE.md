# プロジェクトルール

## ゲーム作成時の必須手順

HTML ゲームファイルを新規作成・更新したら、**必ず**以下を実行すること：

1. Gofile にアップロードする
   ```bash
   SRV=$(curl -s https://api.gofile.io/servers | jq -r '.data.servers[0].name')
   LINK=$(curl -s -F "file=@<ファイルパス>" "https://${SRV}.gofile.io/uploadFile" | jq -r '.data.downloadPage')
   ```
2. チャットにリンクを明示する（例: `🎮 Gofile: https://gofile.io/d/XXXXX`）
