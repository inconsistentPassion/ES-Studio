#include <cstdio>
#include <iostream>
#include <thread>
#include <algorithm>
#include <iterator>
#include <nlohmann/json.hpp>

#include "comPipe.h"
#include "MinHook.h"
#include "RTTI.h"
#include "SimHooks.h"
#include "Helpers.h"

using json = nlohmann::json;

static EngineEdit* engineEdit;
static EngineUpdate* engineUpdate;
static SimHooks* simHooks;
static SimFunctions* simFunctions;
static Helpers* helpers;
static Globals* _g;

void sendJson() {
    json j;

    j["Status"] = engineUpdate->Status;
    j["Name"] = engineUpdate->Name;
    j["cylinderCount"] = engineUpdate->cylinderCount;
    j["RPM"] = engineUpdate->RPM;
    j["maxRPM"] = engineUpdate->maxRPM;
    j["sparkAdvance"] = engineUpdate->sparkAdvance;
    j["tps"] = engineUpdate->tps;
	j["vehicleSpeed"] = engineUpdate->vehicleSpeed;
    j["sparkTimingList"] = engineUpdate->sparkTimingList;
    j["manifoldPressure"] = engineUpdate->manifoldPressure;
    j["gear"] = engineUpdate->gear;
    j["clutchPosition"] = engineUpdate->clutchPosition;
    j["atLimiter"] = engineUpdate->atLimiter;
    j["twoStepActive"] = engineUpdate->twoStepActive;
    j["engineLoad"] = engineUpdate->engineLoad;
    j["power"] = engineUpdate->power;
    j["torque"] = engineUpdate->torque;
    j["airSCFM"] = engineUpdate->airSCFM;
    j["afr"] = engineUpdate->afr;
    j["temperature"] = engineUpdate->temperature;

    std::string pre = j.dump();
    pre.erase(std::remove(pre.begin(), pre.end(), '\n'), pre.cend());
    const char* result = pre.c_str();
    pipeSendText(result);
}

void UpdateParams() {
    engineUpdate->Status = "Connected";
}

void HandleUpdate(const char* input) {
    try {
        json data = json::parse(input);
        
        if (data.contains("sparkAdvance")) engineEdit->sparkAdvance = data["sparkAdvance"];
        if (data.contains("useIgnTable")) engineEdit->useIgnTable = data["useIgnTable"];
        if (data.contains("customSpark")) engineEdit->customSpark = data["customSpark"];
        if (data.contains("useRpmTable")) engineEdit->useRpmTable = data["useRpmTable"];
        if (data.contains("customRevLimit")) engineEdit->customRevLimit = data["customRevLimit"];
        if (data.contains("useCylinderTable")) engineEdit->useCylinderTable = data["useCylinderTable"];
        if (data.contains("useCylinderTableRandom")) engineEdit->useCylinderTableRandom = data["useCylinderTableRandom"];
        if (data.contains("activeCylinderCount")) engineEdit->activeCylinderCount = data["activeCylinderCount"];
        if (data.contains("activeCylindersRandomUpdateTime")) engineEdit->activeCylindersRandomUpdateTime = data["activeCylindersRandomUpdateTime"];
        if (data.contains("quickShiftEnabled")) engineEdit->quickShiftEnabled = data["quickShiftEnabled"];
        if (data.contains("quickShiftTime")) engineEdit->quickShiftTime = data["quickShiftTime"];
        if (data.contains("quickShiftRetardTime")) engineEdit->quickShiftRetardTime = data["quickShiftRetardTime"];
        if (data.contains("quickShiftRetardDeg")) engineEdit->quickShiftRetardDeg = data["quickShiftRetardDeg"];
        if (data.contains("quickShiftMode")) engineEdit->quickShiftMode = data["quickShiftMode"];
        if (data.contains("quickShiftAutoClutch")) engineEdit->quickShiftAutoClutch = data["quickShiftAutoClutch"];
        if (data.contains("quickShiftCutThenShift")) engineEdit->quickShiftCutThenShift = data["quickShiftCutThenShift"];
        if (data.contains("autoBlipEnabled")) engineEdit->autoBlipEnabled = data["autoBlipEnabled"];
        if (data.contains("autoBlipThrottle")) engineEdit->autoBlipThrottle = data["autoBlipThrottle"];
        if (data.contains("autoBlipTime")) engineEdit->autoBlipTime = data["autoBlipTime"];
        if (data.contains("dsgFarts")) engineEdit->dsgFarts = data["dsgFarts"];
        if (data.contains("twoStepEnabled")) engineEdit->twoStepEnabled = data["twoStepEnabled"];
        if (data.contains("disableRevLimit")) engineEdit->disableRevLimit = data["disableRevLimit"];
        if (data.contains("rev1")) engineEdit->rev1 = data["rev1"];
        if (data.contains("rev2")) engineEdit->rev2 = data["rev2"];
        if (data.contains("rev3")) engineEdit->rev3 = data["rev3"];
        if (data.contains("useCustomIgnitionModule")) engineEdit->useCustomIgnitionModule = data["useCustomIgnitionModule"];
        if (data.contains("twoStepLimiterMode")) engineEdit->twoStepLimiterMode = data["twoStepLimiterMode"];
        if (data.contains("twoStepCutTime")) engineEdit->twoStepCutTime = data["twoStepCutTime"];
        if (data.contains("twoStepRetardDeg")) engineEdit->twoStepRetardDeg = data["twoStepRetardDeg"];
        if (data.contains("twoStepSwitchThreshold")) engineEdit->twoStepSwitchThreshold = data["twoStepSwitchThreshold"];
        if (data.contains("allowTwoStepInGear")) engineEdit->allowTwoStepInGear = data["allowTwoStepInGear"];
        if (data.contains("idleHelper")) engineEdit->idleHelper = data["idleHelper"];
        if (data.contains("idleHelperRPM")) engineEdit->idleHelperRPM = data["idleHelperRPM"];
        if (data.contains("idleHelperMaxTps")) engineEdit->idleHelperMaxTps = data["idleHelperMaxTps"];
        if (data.contains("speedLimiter")) engineEdit->speedLimiter = data["speedLimiter"];
        if (data.contains("speedLimiterSpeed")) engineEdit->speedLimiterSpeed = data["speedLimiterSpeed"];
        if (data.contains("speedLimiterMode")) engineEdit->speedLimiterMode = data["speedLimiterMode"];
        if (data.contains("useAfrTable")) engineEdit->useAfrTable = data["useAfrTable"];
        if (data.contains("targetAfr")) engineEdit->targetAfr = data["targetAfr"];
        if (data.contains("loadCalibrationMode")) engineEdit->loadCalibrationMode = data["loadCalibrationMode"];
        if (data.contains("doubleCamSpeed")) engineEdit->doubleCamSpeed = data["doubleCamSpeed"];
        if (data.contains("dfcoEnabled")) engineEdit->dfcoEnabled = data["dfcoEnabled"];
        if (data.contains("dfcoExitRPM")) engineEdit->dfcoExitRPM = data["dfcoExitRPM"];
        if (data.contains("dfcoSpark")) engineEdit->dfcoSpark = data["dfcoSpark"];
        if (data.contains("dfcoEnterDelay")) engineEdit->dfcoEnterDelay = data["dfcoEnterDelay"];
    }
    catch (const json::exception& e) {
        std::cerr << "JSON Exception in HandleUpdate: " << e.what() << std::endl;
    }
}

void PipeReader() {
    while (true) {
        HANDLE hPipe;
        char buffer[10240];
        DWORD dwRead;

        hPipe = CreateNamedPipe(TEXT("\\\\.\\pipe\\est-output-pipe"),
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            1,
            10240 * 16,
            10240 * 16,
            NMPWAIT_USE_DEFAULT_WAIT,
            NULL);
        while (hPipe != INVALID_HANDLE_VALUE)
        {
            if (ConnectNamedPipe(hPipe, NULL) != FALSE)
            {
                while (ReadFile(hPipe, buffer, sizeof(buffer) - 1, &dwRead, NULL) != FALSE)
                {
                    buffer[dwRead] = '\0';
                    HandleUpdate(buffer);
                }
            }

            DisconnectNamedPipe(hPipe);
        }
    }
}

void Update() {
    while (true) {
        UpdateParams();
        helpers->buildSparkList();
        helpers->UpdateEngineType();
        sendJson();;
    }
}

void openConsole() {
    AllocConsole();
    FILE* fDummy;
    //freopen_s(&fDummy, "CONIN$", "r", stdin);
    freopen_s(&fDummy, "CONOUT$", "w", stderr);
    freopen_s(&fDummy, "CONOUT$", "w", stdout);
}

void Main() {
    #ifdef _DEBUG
    openConsole();	
    #endif
    printf("ES Client Loaded!\n");

    MH_Initialize();
    _g = new Globals();
    engineEdit = new EngineEdit();
    engineUpdate = new EngineUpdate();
    simFunctions = new SimFunctions();
    simHooks = new SimHooks(engineUpdate, engineEdit, simFunctions, _g);
    helpers = new Helpers(engineUpdate, engineEdit, simFunctions, _g);

    std::thread update(Update);
    std::thread reader(PipeReader);
    update.detach();
    reader.detach();
}

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        CreateThread(0, 0, (LPTHREAD_START_ROUTINE)Main, hModule, 0, 0);
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

