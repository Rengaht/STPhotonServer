using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace STPhotonServer
{
    class STActionCode
    {
    }
    public enum STClientCode
    {
        APP_Check_Id=51,
        APP_Join=52,

        APP_Set_Side=60,
        APP_Set_Name=61,
        APP_Set_House=62,
        APP_Blow=63,
        APP_Light=64,
        APP_Shake=65,

        APP_Rotate=71,

        APP_Face=81,

        LED_Join=101,
        LED_Score=102


    }
    public enum STServerCode
    {
        CLogin_Success=150,
        Id_And_Game_Info=151,
        
        CJoin_Success=152,

        CSend_GG=153,

        CSet_Side_Success=160,
        CSet_Name_Success=161,
        CSet_House_Success=162,

        
        CGameB_Start=171,
        
        CSet_Face_Success=181,

        //LGame_Info=201,

        LConnected=201,
        LRequest_Score=202,
        LSend_Score_Success=203,
        LSend_GG=204,
        

        LSet_Name=211,
        LSet_House=212,
        LSet_Blow=213,
        LSet_Light = 214,
        LSet_Shake = 215,


        LGameB_Start=220,
        LUser_Color=221,
        LSet_Rotate=222,

        LSet_Face=230
       
        
    }
   
}
