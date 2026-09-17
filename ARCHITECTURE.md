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

Scanly tar 299 kr per månad och kund. Vid lansering med 30 kunder blir intäkten ungefär 8 970 kr per månad och vid 200 kunder blir den ungefär 59 800 kr per månad. Scenariot räknar med cirka 15 000 fakturor per månad vid lansering och cirka 100 000 efter ett år. Om jag räknar med ungefär en sida per faktura uppskattar jag kostnaden för Document Intelligence till cirka 1 500 kr per månad vid lansering och cirka 9 800 kr vid den större volymen. Med Container Apps, ACR och Blob Storage inräknat uppskattar jag den totala Azure-kostnaden till ungefär 1 650–1 900 kr respektive 10 200–10 800 kr per månad. Det blir ungefär 0,10–0,12 kr i Azure-kostnad per analyserad faktura med mina antaganden. Den största kostnaden och troliga flaskhalsen när trafiken ökar är Document Intelligence eftersom den kostnaden ökar med antalet analyserade sidor, medan Blob Storage kostar relativt lite.