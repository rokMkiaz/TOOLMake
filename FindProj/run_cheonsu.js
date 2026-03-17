const fs = require('fs');
const path = require('path');
const MongoDB = require('mongodb').MongoClient;
const CONFIG = require('./Config');
const CONFIG_DATA = require('./ConfigData');

function getMongoUrl(serverkey, name) {
    const find_obj = CONFIG_DATA[CONFIG.mode].mongodb_config[serverkey].find(e => e.name === name);
    if (!find_obj) return null;
    return `mongodb://${find_obj.id}:${find_obj.pw}@${find_obj.ip}:${find_obj.port}?authSource=admin&authMechanism=SCRAM-SHA-1`;
}

function buildHtml(query2, generatedAt) {
    const rows = query2.map(r => `
        <tr>
            <td>${r.charunique}</td>
            <td>${r.charname}</td>
            <td>${r.play_date_kst}</td>
            <td>${r.total_playtime_sec.toLocaleString()}</td>
            <td>${r.total_playtime_hour}</td>
        </tr>`).join('');

    return `<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="UTF-8">
<title>천수도 조사 집계</title>
<style>
  body { font-family: 맑은 고딕, sans-serif; padding: 20px; background: #f5f5f5; }
  h2 { color: #333; }
  .info { color: #666; font-size: 13px; margin-bottom: 12px; }
  table { border-collapse: collapse; background: #fff; }
  th { background: #2c5f8a; color: #fff; padding: 8px 14px; text-align: center; }
  td { padding: 6px 14px; border-bottom: 1px solid #ddd; text-align: center; }
  tr:nth-child(even) td { background: #f0f6fc; }
  tr:hover td { background: #d6eaf8; }
  td:nth-child(4), td:nth-child(5) { text-align: right; }
</style>
</head>
<body>
<h2>천수도 조사 집계 (logtype:34 / mapindex:1209)</h2>
<p class="info">생성시각: ${generatedAt} &nbsp;|&nbsp; 총 ${query2.length}건 (timestamp &gt; 1772067600)</p>
<table>
  <thead>
    <tr>
      <th>charunique</th>
      <th>charname</th>
      <th>play_date_kst</th>
      <th>total_playtime_sec</th>
      <th>total_playtime_hour</th>
    </tr>
  </thead>
  <tbody>
${rows}
  </tbody>
</table>
</body>
</html>`;
}

async function main() {
    const DB_NAME = '1003b_log112';
    console.log(`[1/3] MongoDB 접속 중... (${DB_NAME})`);

    const connectMongo = await MongoDB.connect(getMongoUrl('world', DB_NAME));

    console.log('[2/3] 데이터 조회 중... (logtype:34, mapindex:1209)');
    const allDocs = [];
    await connectMongo.db(DB_NAME).collection('char_logs')
        .find({ logtype: 34, 'userinfo.mapindex': 1209 })
        .forEach(doc => {
            allDocs.push({
                accunique:  doc.accunique,
                charname:   doc.charname,
                charunique: doc.charunique,
                playtime:   doc.playtime,
                timestamp:  doc.timestamp,
            });
        });

    await connectMongo.close();
    console.log(`    → ${allDocs.length}건 수신`);

    // 집계: timestamp > 1772067600, KST 날짜별 playtime 합산
    const groups = {};
    allDocs.filter(d => d.timestamp > 1772067600).forEach(d => {
        const kstDate = new Date((d.timestamp + 9 * 3600) * 1000).toISOString().slice(0, 10);
        const key = `${d.charunique}|${d.charname}|${kstDate}`;
        if (!groups[key]) {
            groups[key] = { charunique: d.charunique, charname: d.charname, play_date_kst: kstDate, total_playtime_sec: 0 };
        }
        groups[key].total_playtime_sec += d.playtime;
    });

    const query2 = Object.values(groups)
        .map(g => ({
            charunique:          g.charunique,
            charname:            g.charname,
            play_date_kst:       g.play_date_kst,
            total_playtime_sec:  g.total_playtime_sec,
            total_playtime_hour: (g.total_playtime_sec / 3600).toFixed(4),
        }))
        .sort((a, b) =>
            a.charunique !== b.charunique
                ? a.charunique - b.charunique
                : a.play_date_kst.localeCompare(b.play_date_kst)
        );

    console.log(`[3/3] HTML 생성 중... (집계 ${query2.length}건)`);
    const generatedAt = new Date().toLocaleString('ko-KR');
    const html = buildHtml(query2, generatedAt);
    const outPath = path.join(__dirname, 'outputlog', 'result.html');
    fs.writeFileSync(outPath, html, 'utf8');
    console.log(`    → 저장 완료: ${outPath}`);
}

main().catch(err => { console.error(err); process.exit(1); });
