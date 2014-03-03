# Dirigent
## Overview
Dirigent is a remote application management tool for a set of applications running on one or multiple networked computers. It runs on .net and Mono platforms, supporting both Windows and Linux operating systems.

It allows launching a given set of applications in given order according to predefined launch plan. The plan specifies what applications to launch, on what computers, in what order and whether to wait for a dependent apps to become fully initialized.

Applications can be remotely started, terminated or restarted, either individually or all-at-once as defined in current launch plan. 

The applications are continuously monitored whether they are already initialized and still running. Their status can be displayed on a control GUI. An application that is supposed to run continuously can be automatically restarted after crash.

The launch order is defined either by a fixed ordinal number assigned to an application or it is determined automatically based on the dependecies among applications.

Applications can be set to run on computer startup. To speedup the system startup process, certain applications can be launched even before the connection among computers is estabilished and - those apps that do not depend on other apps running on other computers.


## Architecture

### Agents and master

Each computer is running a background process - agent. One of the computers runs a master. Agents connect to master who is the central hub providing data interchange between agents.

Agents control the processes running locally - take care of local application launching, killing, restarting and status monitoring. 

Agents listens to and executes application management commands from master.

Agents publish the status of local applications to master who in turn spreads it to all other agents. The status include whether the app is running, whether it is already initialized etc.

Master is another background process managing the configuration of applications (launch plans etc.) The same configuration is shared among all agents.

### Control GUI
The control GUI is a standalone application connected to master.

Before manually executing a launch plan it can be quickly customized by removing some of the applications from the launch sequence.



#### Launch plan
Launch plan is just a list of apps to be launched in given order. Just one plan at a time is active.

Each app in the launch plan has the following attributes:

 - unique text id of the application; togeher with the machine id it makes a unique id
 - application binary file full path
 - startup directory
 - command line arguments
 - the launch order in case of same priority of multiple apps
 - whether to automatically restart the app after crash
 - what computer to launch the application on (unique machine id as text string)
 - what apps is this one dependent on, ie. what apps have to be launched and fully initalized before this one can be started
 - a mechanism to detect that the app is fully initialized (by time, by a global mutex, by exit code etc.)
 
#### Templated launch plan definition
Plan definition in an XML file uses so called templates allowing the inheritance of attributes. Every record in the plan can reference a template record. From the template all the attributes are loaded and only then they can ge overwritten by equally named attributes loaded from the referencing entry. The template record can reference another more generic template records.

#### Computer list
For each computer there is a textual machine id and the IP address defined. One of the machines is marked as master. Such computer will run not just agent process but also the master process.

#### Autodetection of the machine id
By comapring the computer's IP address with those available in the computer list the dirigent processes automaticaly determine on what machine they are running. There is no need to tell them what machine id they are going to use.


# Design notes in Czech
### Detekce dokonèení inicializace aplikace
Jak dirigent pozná, e aplikace je ji inicializována a e mùeme zaèíst spouštìt další, na ní závislé aplikace?

U kadé aplikace lze definovat pro ni specifickı mehcanismus. Mùe to bıt:

 - podle èasu od spuštìní
 - podle globálního synchronizaèního objektu

Kadı lokální dirigent distribuuje ostatním informace o inicializovanosti aplikací. 

## Podpora v aplikacích
Nìkterım aplikací trvá spouštìní a inicializace dlouhou dobu. U takovıch aplikací èistì jen podle èasu od spuštìní nelze poznat, zda ji je aplikace plnì funkèní a e se tedy mohou spouštìt další, na ní závislé, aplikace). Aplikace mùe dát najevo, e je ji inicializována, napø. pomocí globálního synchronizaèního objektu (napø. mutexu). Tento je sledován lokálním dirigentem.
 

    ## Ovládání dirigenta
Dirigenta lze ovládat nìkolika zpùsoby, z nich kadı se hodí pro jinou pøíleitost. Všechno lze dìlat interaktivnì z vestavìného dirigentova GUI. Nebo lze dávat dirigentovi povely spouštìním jeho pøíkazového agenta a pøedáním mu povelù na pøíkazové øádce. Takté se lze na mastera napojit po síti nìkterım ze standardních protokolù podporovanıch ve WCF.
 
# Implementace
 
### Repozitáø stavu aplikací
 
mapa appid na strukturu app state
 
App state obsahuje

  - aplikace bìí, PID
  - aplikace ji inicializována
  - odkaz na spouštìcí plán
 
Konfiguraèní strom v pamìové, typovì bezpeèné podobì

 - spouštìcí plány
 - seznam poèítaèù
 

#### Aktualizace stavu aplikací

Projedou se všechny domnìle bìící aplikace ze spouštìcího plánu. Ovìøí se, e jejich PID stále existuje. Provìøí se podmínka inicializace (èas, mutex...)

#### Provádìní spouštìcího plánu

Nejprve dojde k ukonèení aktuálního plánu, tedy k pozabíjení aplikací. Z nového plánu se vyrobí poloky aplikaèního repozitáøe ve stavu "nespuštìno".

Vyhodnotí se poøadí spouštení aplikací. Vısledkem je seznam jenotlivıch spouštìcích "vln". Vlna obsahuje aplikace, jejich závislosti ji byly uspokojeny pøedchozí vlnou. V první vlnì jsou aplikace, které nejsou závislé na nièem. V druhé vlnì aplikace závislé na tìch z první vlny. Ve tøetí jdou aplikace závislé na tìch z druhé vlny a tak dále.

Spouští se jedna vlna po druhé, dokud nebìí vše. Další vlna se však spustí a tehdy, pokud jsou splnìny všechny její podmínky - tj. e jsou ji inicializované aplikace z pøedchozí vlny.

Pokud se nìkterá z aplikací nepodaøí spustit, dirigent (v závislosti na nastavení té které aplikace) mùe pokus o spuštìní i nìkolikát opakovat.

Skonèí-li všechny pokusy o spuštìní nezdarem, provádìní spouštìcího plánu se zastaví a nahlásí se chyba. Chyba se hlásí zadavateli povelu pro spouštìní plánu. Pokud byl plán spuštìn z dirigentova GUI, objeví se chyba v tomto GUI. Pokud o spuštìní plánu poádala jiná aplikace (napø. pøes sí), dotsane chybovou zprávu zpìt po stejném kanálu.


#### Zprávy mezi masterem a agenty

Master komunikuje s agenty pomocí zpráv. Master se chová, jako by v nìm bìel agent, a posílá urèené agentùm i sám sobì.

 - **Proveï spouštìcí plán.** Master ádá všechny agenty o zahájení provádìní daného spouštìcího plánu. Agenti rozhodnou o poøadí aplikací (vypoètou vlny; kadı vychází ze stejné sdílené konfigurace) a zaènou spouštìt aplikace. Prùbìnì aktualizují stav aplikací z plánu. Neskonèí, dokud nejsou spuštìné všechny nebo nedostanou povel k ukonèení plánu. Úspìšné spuštìní všeho nijak zvláš nesignalizuje, master to pozná sám ze sdíleného stavu aplikací.

 - **Ukonèi aktuální plán.** Master posílá agentùm povel k ukonèení všech aplikací dosud spuštìnıch v rámci aktuálního plánu. Agenti aplikace pozabíjí a aktualizují sdílenı stav aplikací.

 - **Stav lokálních aplikací.** Agent posílá masterovi stav svıch lokálních aplikací. Master si zaktualizuje stav aplikací v repozitáøi a rozešle stav všech aplikací všem agentùm.

 - **Stav všech aplikací.** Master posílá všem agentùm stav všech aplikací tak, jak jej dostal od jednotlivıch agentù. 

 - **Chyba spouštìní plánu.** Agent informuje mastera o selhání jeho èásti spouštìcího plánu.

 - **Restartuj zvolenou aplikaci.** Master posílá agentovi poadavek na ukonèení a následné spuštìní vybrané aplikace z konkrétního plánu.

 - **Ukonèi zvolenou aplikaci.** Master posílá agentovi poadavek na ukonèení aplikace. Agent aplikaci zabije a aktualizuje sdílenı stav aplikace.

 - **Spus zvolenou aplikaci.** Master posílá agentovi poadavek na spuštìní zvolené aplikace z aktuálního plánu. Agent aplikaci spustí a aktualizuje sdílenı stav aplikace.

 - Pøedej poadavek masterovi. Agent ádá mastera o provedení akce, kterou potenciálnì nezvládne sám (napø. spuštìní aplikace najiném poèítaèi). Master poadavek pøepošle na agenta nebo agenty, kteøí poadavek dokáí splnit.



 