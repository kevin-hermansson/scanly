# Scanly – Architecture

## 1. Azure Container Apps vs AKS

Vi valde Azure Container Apps eftersom Scanly är ett relativt litet API och vi inte behöver hantera ett helt Kubernetes-kluster själva. Container Apps gör att vi fortfarande kan köra applikationen som en Docker-container, men Azure sköter mer av infrastrukturen åt oss. AKS hade gett oss mer kontroll över till exempel pods, networking och Kubernetes-konfiguration, men hade också gjort lösningen mer komplex att administrera. För Scanly känns Container Apps därför mer lämpligt eftersom vi främst behöver kunna deploya API:t, skala det och exponera det via en publik endpoint. Lösningen har minst två replicas, vilket också gör att flera instanser av API:t kan köras samtidigt.

## 2. CI/CD-flöde

Vårt CI/CD-flöde använder GitHub tillsammans med Azure DevOps Pipelines. När kod pushas till `main` startar pipelinen automatiskt. Den kör först restore, build och test för .NET-lösningen. Om de stegen lyckas byggs en Docker-image som pushas till Azure Container Registry. Därefter uppdateras Azure Container App så att den kör den nya imagen. På så sätt blir en grön pipeline samma sak som att den senaste fungerande versionen av Scanly är deployad i Azure.

## 3. Bicep, Infrastructure as Code och idempotens

Vi använder Bicep för att beskriva Scanlys Azure-infrastruktur som kod i stället för att skapa resurser manuellt i portalen. I `main.bicep` definierar vi bland annat Storage Account, Blob Storage, Azure Container Registry, Managed Identity, Container Apps Environment och Container App. Fördelen med Infrastructure as Code är att miljön blir enklare att återskapa och ändringar kan versionshanteras tillsammans med resten av projektet. Innan deployment använder vi `az deployment group what-if` för att kontrollera vilka resurser Azure tänker skapa eller ändra. Bicep är också idempotent, vilket betyder att samma template kan köras flera gånger utan att skapa dubbla resurser så länge konfigurationen inte har ändrats.

## 4. Säkerhet

Scanly använder Managed Identity för att undvika att lagra lösenord eller nycklar direkt i koden. Container Appen har en egen identitet som får de behörigheter den behöver via RBAC. Den får till exempel rättighet att läsa Docker-imagen från Azure Container Registry och att läsa och skriva data i Blob Storage. Storage Accountet tillåter inte publik blob access och använder minst TLS 1.2. På det sättet hålls åtkomsten mellan Azure-resurserna inom Azure och vi slipper hårdkodade secrets i GitHub eller i applikationen.
## 5. Ekonomi

Scanlys kostnader består främst av Azure Container Apps, Azure Container Registry, Blob Storage och Azure Document Intelligence. Container Apps debiteras efter resursanvändning och antal requests, men har även en kostnadsfri nivå varje månad. Azure Container Registry körs på Basic-nivå, vilket är tillräckligt för projektet eftersom vi bara behöver lagra ett mindre antal Docker-images. Blob Storage blir relativt billigt eftersom varje analyserad faktura bara sparas som en liten JSON-fil.

Den största rörliga kostnaden blir Azure Document Intelligence eftersom tjänsten debiteras per analyserad sida. Därför ökar kostnaden ungefär i takt med hur många fakturor som analyseras. För Scanly är det viktigt att jämföra Azure-kostnaden per faktura med intäkten per kund för att se om affärsmodellen är hållbar. Den exakta månadskostnaden räknas ut separat för lanseringsscenariot och den större kundvolymen.