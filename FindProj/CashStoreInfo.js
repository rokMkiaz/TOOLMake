
const IS_SALE_POS = 5;              //1003bM_CashStoreInfo.csv 문서에서 판매상태 여부 탭위치 번호
const PRODUCT_CODE_POS = 24;        //1003bM_CashStoreInfo.csv 문서에서 상품코드 탭위치 번호
const PRODUCT_ID_POS = 0;
const PRODUCT_NAME_POS = 4;
const PRODUCT_ANDROID_POS = 14;
const PRODUCT_APPLE_POS = 15;
const PRODUCT_MILEAGE = 19;
const PRODUCT_CODE_GOOGLE_STORE = 20;
const PRODUCT_CODE_APPLE_STORE = 21;
const PRODUCT_CODE_ONE_STORE = 22;
const PRODUCT_STORAGE_TYPE = 45       //보관함 위치

const MARKET_ID_APPLE = 'apple_store';
const MARKET_ID_GOOGLE = 'google_store';
const MARKET_ID_GALAXY = 'galaxy_store';
const MARKET_ID_ONESTORE = 'one_store';

module.exports = function( dataArray ){
    let m_Data = new Map();

    (function(){
        dataArray.forEach(element => {
            if( Number.isInteger(element[PRODUCT_ID_POS]) )
                m_Data.set(element[PRODUCT_ID_POS], element);
        });
    })()

    let getData = (procduct_id) => {
        return m_Data.get(procduct_id);
    }

    const getDataProductCode = (market_id, market_product_code) => {
        for (let value of m_Data.values()) {
            if( MARKET_ID_APPLE == market_id ){
                if (value[PRODUCT_CODE_APPLE_STORE] == market_product_code )
                    return value;
            }

            else if( MARKET_ID_GOOGLE == market_id){
                if (value[PRODUCT_CODE_GOOGLE_STORE] == market_product_code )
                    return value;
            }

            else if( MARKET_ID_GALAXY == market_id){
                if (value[PRODUCT_CODE_GOOGLE_STORE] == market_product_code )
                    return value;
            }
            
            else if( MARKET_ID_ONESTORE == market_id){
                if (value[PRODUCT_CODE_ONE_STORE] == market_product_code )
                    return value;
            }            
        }
        return null;
    }

    return{
        getData,
        getDataProductCode,
    }
}