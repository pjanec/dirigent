agentcmd.exe StopPlan

taskkill /im master.exe
taskkill /im agent.exe

timeout /t 3

start master
start agent
