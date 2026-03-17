const PATH = require("path");
const FS = require("fs");
const IS_NUMBER = require('./Utill').isNumeric;
const MongoDB = require('mongodb').MongoClient;
const CONFIG = require('./Config');
const CONFIG_DATA = require('./ConfigData');
const xlsx = require('xlsx');
const fs = require('fs');
const CashStoreInfo = require('./CashStoreInfo');

module.exports = (() => {
    const PACKAGE = [
        {index:1002,name:'��Ī ��� ���� Ǯ ��Ű��',gold:2000,items:[[6001,5],[6027,5],[2991,20],[6035,10],[3906,100],[5003,100],[3502,20],[3219,10],[3258,10],[6044,10]]},
        {index:1011,name:'��Ī ��� ���� ��Ű��',gold:750,items:[[6001,5],[6027,5],[2991,100],[3219,50],[3258,50],  [5003,	200],	[3502,	50]	,[6042,	20]]},
        {index:1006,name:'�մ����� ���� ��Ű��',gold:1250,items:[[6003,1],[3219,10],[3258,10],[6901,10],[6903,6],[6044,5]]},
        {index:112,name:'�ְ� ������ ���� ��Ű��',gold:750,items:[[5003,380],[6044,3]]},
        {index:1003,name:'���� ���� ��Ű��1',gold:300,items:[[6035,7],[3906,70],[6044,1]]},
        {index:1004,name:'���� ���� ��Ű��2',gold:750,items:[[6027,2],[6034,2],[6044,3]]},
        {index:1005,name:'���� ���� ��Ű��3',gold:1250,items:[[6001,5],[6032,1],[6044,5]]},
        {index:101,name:'���� ��ȯ ��Ű��',gold:5450,items:[[6001,3],[6027,5],[6044,10]]},
        {index:111,name:'�ְ� ��ȯ ��Ű��',gold:750,items:[[6001,1],[6027,2],[6044,3]]},
        {index:1012,name:'��Ī ��� ��ȯ�� ��Ű��',gold:500,items:[[6001,3],[6027,3]]},
        {index:1001,name:'��Ī ��� �ʺ��� ��Ű��',gold:150,items:[[6001,1],[6027,1]]}, 
    ];

    var Instance = null;            //�ν��Ͻ�
    let _cashstoreInfo = null;
    function saveXLSXWithJSON(data, fileName) {
        const workSheet = xlsx.utils.json_to_sheet(data);
        const stream = xlsx.stream.to_csv(workSheet);
        stream.pipe(fs.createWriteStream(__dirname + '/outputlog/' + fileName + '.csv'));
    }

    function jsonToCSV(json_data) {
        // 1-1. json ������ ��� 
        const json_array = json_data;
        // 1-2. json�����͸� ���ڿ�(string)�� ���� ���, JSON �迭 ��ü�� ����� ���� �Ʒ� �ڵ� ��� 
        // const json_array = JSON.parse(json_data); 

        // 2. CSV ���ڿ� ���� ����: json�� csv�� ��ȯ�� ���ڿ��� ��� ���� 
        let csv_string = '';
        // 3. ���� ����: json_array�� ù��° ���(��ü)���� ����(�Ӹ���)���� ����� Ű���� ���� 
        const titles = Object.keys(json_array[0]);
        // 4. CSV���ڿ��� ���� ����: �� ������ �ĸ��� ����, ������ ������ �ٹٲ� �߰� 
        titles.forEach((title, index) => { csv_string += (index !== titles.length - 1 ? `${title},` : `${title}\r\n`); });
        // 5. ���� ����: json_array�� ��� ��Ҹ� ��ȸ�ϸ� '����' ���� 
        json_array.forEach((content, index) => {
            let row = '';
            // �� �ε����� �ش��ϴ� '����'�� ���� �� 
            for (let title in content) {
                // for in ���� ��ü�� Ű���� �����Ͽ� ��ȸ��. 
                // �࿡ '����' �Ҵ�: �� ���� �տ� �ĸ��� �����Ͽ� ����, ù��° ������ �տ� �ĸ�X 
                row += (row === '' ? `${content[title]}` : `,${content[title]}`);
            }
            // CSV ���ڿ��� '����' �� ����: �ڿ� �ٹٲ�(\r\n) �߰�, ������ ���� �ٹٲ�X 
            csv_string += (index !== json_array.length - 1 ? `${row}\r\n` : `${row}`);
        }) // 6. CSV ���ڿ� ��ȯ: ���� �����(string) 
        return csv_string;
    }

    async function UVCharLog(){
        try{
            const DB_NAME = 'chosun_logUV';
            let dbinfo = getMongoUrl('world', DB_NAME);
            let connectMongo = await MongoDB.connect(dbinfo);
            if (connectMongo) {
                let ccus = [];
                await connectMongo.db(DB_NAME).collection('uv_logs').find({"timestamp":{"$gte":1646233200,"$lt":1646319600}})
                .forEach(function (doc) {
                    let dtLogtime = new Date(((doc.timestamp+32400) * 1000));
                    doc.time = dtLogtime.toISOString();
                    let input = {};
                    for (variable in doc) {
                        if (variable == '_id')
                            continue;
                        input[variable] = doc[variable];
                    }
                    ccus.push(input);
                });
                saveXLSXWithJSON(ccus, DB_NAME);
            }
        }

        catch(error){
            console.error(error);
        }
    }



    
    async function charlog_22613(){
        try{
            const DB_NAME = '1003b_log112';
            let dbinfo = getMongoUrl('world', DB_NAME);
            let connectMongo = await MongoDB.connect(dbinfo);
            if (connectMongo) {
                let allDocs = [];
                await connectMongo.db(DB_NAME).collection('char_logs').find({logtype:34,'userinfo.mapindex':1209})
                .forEach(function (doc) {
                    allDocs.push({
                        accunique:  doc.accunique,
                        charname:   doc.charname,
                        charunique: doc.charunique,
                        playtime:   doc.playtime,
                        timestamp:  doc.timestamp,
                    });
                });

                // 쿼리1: timestamp > 1772766000 인 레코드 추출
                const query1 = allDocs
                    .filter(d => d.timestamp > 1772766000)
                    .map(d => ({
                        accunique:  d.accunique,
                        charname:   d.charname,
                        charunique: d.charunique,
                        playtime:   d.playtime,
                        timestamp:  d.timestamp,
                    }));

                // 쿼리2: timestamp > 1772067600, charunique+charname+KST날짜 기준 playtime 합산
                const groups = {};
                allDocs
                    .filter(d => d.timestamp > 1772067600)
                    .forEach(d => {
                        const kstDate = new Date((d.timestamp + 9 * 3600) * 1000)
                            .toISOString().slice(0, 10);
                        const key = `${d.charunique}|${d.charname}|${kstDate}`;
                        if (!groups[key]) {
                            groups[key] = {
                                charunique:         d.charunique,
                                charname:           d.charname,
                                play_date_kst:      kstDate,
                                total_playtime_sec: 0,
                            };
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

                saveXLSXWithJSON(query1, '천수도_조사_필터');
                saveXLSXWithJSON(query2, '천수도_조사_집계');
                console.log(`완료: 필터 ${query1.length}건, 집계 ${query2.length}건`);
            }
        }
        catch(error)
        {
            console.error(error);
        }
    }

    function ReadData( file_Name ){
        let dataArray = [];
        var filePath = PATH.join(__dirname, `./_data/${file_Name}`);
        var data = FS.readFileSync(filePath, {encoding: "utf8"});
        var rows = data.split("\n");
        
        for (var rowIndex in rows) {
            var row = rows[rowIndex].split("\t");
            if( row[0].toString().substring(0,1) == '' || row[0].toString().substring(0,1) == ';' /*|| 1 != Number( row[IS_SALE_POS] )*/){//�Ǹ����� �ƴϸ� ���� �ʴ´�.
                continue;
            }
            else {
                var data = [];                  // �� ��ü�� �����ϰ� ���⿡ �����͸� �߰��Ѵ�.
                for (var columnIndex in row) {  // Į�� ������ŭ ���鼭 ������ ������ �߰��ϱ�.
                    data.push(IS_NUMBER(row[columnIndex]) ? Number(row[columnIndex]) : row[columnIndex]);
                }
                dataArray.push(data);
            }
        }
        return dataArray;
    }    
    async function Init() {
        //_cashstoreInfo = new CashStoreInfo( ReadData('1003bM_CashStoreInfo.csv'));
        await charlog_22613();
        //await UV();
        //await MoveLog();
        //await CharLog();
        //await CharLog2();
        return;
        const DB_NAME = ['chosun_log1', 'chosun_log2', 'chosun_log3', 'chosun_log4', 'chosun_log5', 'chosun_log6'];
        //const DB_NAME = ['chosun_log2'];

        try {

            
            for( let i = 0; i < DB_NAME.length; i++ )
            {
                let dbinfo = getMongoUrl('world', DB_NAME[i]);
                let connectMongo = await MongoDB.connect(dbinfo);
                if (connectMongo) {
                    let ccus = [];
                    await connectMongo.db(DB_NAME[i]).collection('ccu_logs').find({ timestamp: { $gte: 1642409383 }, group_num:{$in:[11,12,13,21,22,23,31,32,33,41,42,43,51,52,53,61,62,63]} }).sort({ timestamp: 1, group_num: 1 }).forEach(function (doc) {
                        let dtLogtime = new Date(doc.timestamp * 1000);
                        //doc.date = dtLogtime;
                        doc.time = dtLogtime.toISOString();
                        let input = {};
                        for (variable in doc) {
                            if (variable == '_id' || variable == 'cnt_ccu' || variable == 'sum_ccu')
                                continue;
                            input[variable] = doc[variable];
                            //console.log("key: " + variable + ", value: " + obj[variable]); 
                        }
    
                        ccus.push(input);
                        //let json = JSON.stringify(input);
    
                        //let ddd = JSON.parse(json);
                        //saveXLSXWithJSON(input, 'test');
                        //printjson(doc);
                    });
    
                    //console.log(ccus);
                    saveXLSXWithJSON(ccus, DB_NAME[i]);
                }
            }
            
           /*
            for( let i = 0; i < DB_NAME.length; i++ )
            {
                let dbinfo = getMongoUrl('world', DB_NAME[i]);
                let connectMongo = await MongoDB.connect(dbinfo);
                if (connectMongo) {
                    let ccus = [];
                    await connectMongo.db(DB_NAME[i]).collection('item_logs_210929').find({ charunique:63372,logtype:1 }).sort({ timestamp:1 }).forEach(function (doc) {
                        let dtLogtime = new Date(doc.timestamp * 1000);
                        //doc.date = dtLogtime;
                        //doc.time = dtLogtime.toISOString();
                        let input = {};
                        for (variable in doc) {
                            if (variable == '_id' || variable == 'cnt_ccu' || variable == 'sum_ccu')
                                continue;
                            input[variable] = doc[variable];
                            //console.log("key: " + variable + ", value: " + obj[variable]); 
                        }
    
                        ccus.push(input);
                        //let json = JSON.stringify(input);
    
                        //let ddd = JSON.parse(json);
                        //saveXLSXWithJSON(input, 'test');
                        //printjson(doc);
                    });
    
                    //console.log(ccus);
                    saveXLSXWithJSON(ccus, DB_NAME[i]);
                    console.log('complete!!!');
                }
            }
            */           
        }
        catch (err) {
            console.error(err);
        }
    }

    /*
    function getMongoUrl(name){
        return 'mongodb://' + CONFIG_DATA[CONFIG.mode].mongodb_config[name].id + ':'
        + CONFIG_DATA[CONFIG.mode].mongodb_config[name].pw + '@' 
        + CONFIG_DATA[CONFIG.mode].mongodb_config[name].ip + ':' 
        + CONFIG_DATA[CONFIG.mode].mongodb_config[name].port 
        + '?authSource=admin&authMechanism=SCRAM-SHA-1';
    }
    */

    function getMongoUrl(serverkey, name) {
        let find_obj = CONFIG_DATA[CONFIG.mode].mongodb_config[serverkey].find(element => name == element.name);
        if (find_obj) {
            return 'mongodb://' + find_obj.id + ':'
                + find_obj.pw + '@'
                + find_obj.ip + ':'
                + find_obj.port
                + '?authSource=admin&authMechanism=SCRAM-SHA-1';
        }
        return null;
    }

    function create() {
        return {
            Init,
        };
    }

    return {
        getInstance: function () {
            if (!Instance) Instance = create();
            return Instance;
        }
    };
})();