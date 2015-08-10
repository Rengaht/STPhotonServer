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
        APP_Leave=66,

        APP_Rotate=71,

        APP_Face=81,
        

        LED_Join=101,
        LED_Score=102,
        LED_StartRun=103,
        LED_EatIcon=104,

        LED_SwitchGame=105


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
        CSet_Leave_Success=163,
        
        CGameB_Ready=171,
        CGameB_Start=172,
        CGameB_Eat=173,
        
        CSet_Face_Success=181,

        //LGame_Info=201,

        LConnected=201,
        LRequest_Score=202,
        LSend_Score_Success=203,
        LSend_GG=204,
        

        LAdd_House=211,
        LSet_Name=212,
        LSet_House=213,
        LSet_Blow=214,
        LSet_Light = 215,
        LSet_Shake = 216,
        LSet_User_Leave=217,

        LGameB_Ready=220,
        LGameB_Start=223,

        LUser_Color=221,
        LSet_Rotate=222,

        LSet_Face=230
       
        
    }
   
}
