# Scanly – Architecture

## 1. Azure Container Apps vs AKS

Jag valde Azure Container Apps eftersom Scanly är ett ganska litet API och jag inte behöver ett helt Kubernetes-kluster. Jag kan fortfarande köra API:t som en Docker-container men slipper administrera Kubernetes själv. Container Apps sköter mycket åt mig, till exempel ingress och skalning. Jag har minst två replicas för att applikationen ska kunna köras på flera instanser samtidigt. En nackdel jämfört med AKS är att jag inte får lika mycket kontroll över till exempel nätverk och Kubernetes-konfiguration. Om Scanly hade varit ett mycket större system med många tjänster och mer avancerade krav hade AKS kunnat passa bättre.

## 2. CI/CD-flöde

Koden ligger på GitHub och jag använder Azure DevOps Pipelines för CI/CD. När kod pushas till `main` startar pipelinen automatiskt. Den kör först restore, build och tester på .NET-projektet. Om allt fungerar byggs en Docker-image som pushas till Azure Container Registry. Efter det uppdateras Azure Container App med den nya imagen. Om build eller tester misslyckas kommer pipelinen inte vidare till deployment, vilket gör att den gamla fungerande versionen fortsätter köra i Azure.

## 3. Bicep, Infrastructure as Code och idempotens

Jag använder Bicep för att skapa Azure-resurserna som kod. I Bicep-filen finns bland annat Storage Account, Blob Storage, Azure Container Registry, Managed Identity, Container Apps Environment och Container App. Det gör att jag inte behöver komma ihåg alla steg som annars hade gjorts manuellt i Azure Portal. Jag kan också versionshantera infrastrukturen tillsammans med resten av projektet. Innan deployment har jag använt `az deployment group what-if` för att se vad Azure tänker ändra. Bicep är idempotent, vilket betyder att samma fil kan köras igen utan att Azure skapar nya kopior av alla resurser.

## 4. Säkerhet

Jag använder Managed Identity mellan Container Appen och flera av Azure-resurserna. Identiteten har behörighet via RBAC att läsa images från Azure Container Registry och att läsa och skriva JSON-filer i Blob Storage. Storage Accountet tillåter inte publik blob access och använder minst TLS 1.2. För Azure Document Intelligence använder jag den delade API-nyckeln som kursen tillhandahåller. Nyckeln ligger som ett secret i Container App och finns inte hårdkodad i koden eller i GitHub. Om en secret av misstag skulle pushas till Git behöver nyckeln bytas eller roteras direkt och tas bort från Git-historiken.

## 5. Ekonomi

Scanly tar 299 kr per månad och kund. Vid lansering med 30 kunder blir intäkten ungefär 8 970 kr per månad och jag uppskattar Azure-kostnaden till ungefär 1 650–1 900 kr per månad. Om kundbasen tredubblas till 90 kunder blir det ungefär 45 000 fakturor per månad och då uppskattar jag Azure-kostnaden till ungefär 4 600–5 000 kr per månad. Jag räknar då med ungefär en sida per faktura och att Document Intelligence kostar ungefär 10 USD per 1 000 sidor efter de 1 000 sidor som ingår enligt scenariot. Den dyraste resursen är Document Intelligence eftersom kostnaden ökar nästan direkt med antalet analyserade sidor. Vid lansering blir kostnaden ungefär 0,11–0,12 kr per analyserad faktura. Om trafiken fyrdubblas till ungefär 60 000 fakturor per månad kan både Document Intelligence och gränsen på maximalt tre Container App-replicas bli flaskhalsar, så då hade jag behövt följa belastningen och eventuellt höja skalningsgränsen.