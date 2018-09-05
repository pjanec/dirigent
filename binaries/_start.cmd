Dirigent.AgentCmd.exe StopPlan

taskkill /im Dirigent.Master.exe
taskkill /im Dirigent.Agent.exe

timeout /t 5

start Dirigent.Master
start Dirigent.Agent
