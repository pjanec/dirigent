agentcmd.exe StopPlan

taskkill /im master.exe
taskkill /im agent.exe

timeout /t 10

start master
start agent
