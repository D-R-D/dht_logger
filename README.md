dht22が取得した温湿度データを  
  
python[read data] >--[udp]--> c#[logging] >--[web socket]--> client[use!!!!!!!!]  
  
という感じに流したかったから作った上のc#の部分、変更なしの場合の仕様ポートは下の通り  
udp[port : 15622]  
websocket[port : 60005]
  
変更なしの場合のログファイルは  
/dht_logs/[yyyy]/[MM]/[dd]/[HH].log の形で生成されるよ  
  
ログファイルからのデータ取り出し用にfl_readerを作成してあるけどコミット前に少し仕様変更したので現在は使用不可になってます。  
