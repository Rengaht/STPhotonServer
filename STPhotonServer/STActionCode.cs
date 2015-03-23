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
        APP_Join=51,

        APP_Set_Name=61,
        APP_Blow=62,

        APP_Rotate=71,

        APP_Face=81,

        LED_Join=101,
        LED_Score=102


    }
    public enum STServerCode
    {
        Game_Info=151,
        
        CSend_Id=152,
        CSend_GG=153,

        CSet_Name_Success=161,
        
        CGameB_Start=171,
        
        CSet_Face_Success=181,

        //LGame_Info=201,

        LConnected=201,
        LRequest_Score=202,
        LSend_Score_Success=203,
        LSend_GG=204,
        

        LSet_Name=210,
        LSet_Blow=211,

        LGameB_Start=220,
        LUser_Color=221,
        LSet_Rotate=222,

        LSet_Face=230
       
        
    }
   
}
