agentcmd.exe StopPlan

taskkill /im master.exe
taskkill /im agent.exe

timeout /t 5

start master
start agent
