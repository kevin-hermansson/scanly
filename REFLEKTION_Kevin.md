# Reflektion – Kevin

## 1. Min roll

Jag gjorde uppgiften själv och hade därför ansvar för hela lösningen. Jag arbetade bland annat med API-koden i `Program.cs`, infrastrukturen i `infra/main.bicep` och CI/CD-flödet i `azure-pipelines.yml`. Jag jobbade också med Docker, Azure-resurserna, testerna och dokumentationen. Eftersom jag gjorde allt själv behövde jag både bygga lösningen, felsöka problem och kontrollera att hela flödet fungerade från uppladdad faktura till sparat resultat i Blob Storage.

## 2. Det svåraste momentet

Det svåraste var nog att få alla Azure-delar att fungera tillsammans. Det var inte själva C#-koden som var svårast, utan mer att förstå varför olika fel uppstod i Azure.

Jag hade till exempel problem med Bicep-konfigurationen för Container Apps och fick ändra den innan deployment fungerade. Jag hade också problem när jag testade uppladdning av filer. Först fick jag ett fel med antiforgery när jag använde `POST /invoices`, och senare fick jag ett 403-fel från Blob Storage när jag testade lokalt.

Det lokala 403-felet var ganska lärorikt eftersom Document Intelligence-anropet faktiskt hade fungerat, men min lokala användare hade inte rätt behörighet att skriva till Blob Storage. När samma kod kördes i Azure fungerade det eftersom Container Appens Managed Identity hade rätt RBAC-roll.

## 3. Vad förstår jag nu som jag inte förstod innan?

Jag förstår mycket bättre hur flera Azure-tjänster kan kopplas ihop utan att man behöver lägga lösenord och connection strings överallt.

Tidigare hade jag inte riktigt förstått hur Managed Identity och RBAC fungerade i praktiken. Nu har jag sett hur Container Appen kan få en egen identitet och sedan få specifika rättigheter till exempelvis Azure Container Registry och Blob Storage.

Jag har också fått bättre koll på CI/CD. Nu förstår jag mer konkret vad som händer från att kod pushas till GitHub tills en ny Docker-image faktiskt körs i Azure. Pipelinen kör build och test först, bygger sedan imagen, pushar den till ACR och uppdaterar Container Appen.

Bicep var också något som blev tydligare. I stället för att skapa allt manuellt i Azure Portal kan infrastrukturen beskrivas som kod och köras flera gånger.

## 4. Vad skulle jag göra annorlunda?

Om jag gjorde om projektet hade jag nog planerat strukturen lite mer innan jag började skapa resurser. I början blev det en del fram och tillbaka mellan kod, Azure Portal och Bicep.

Jag hade också försökt få Document Intelligence-delen på plats tidigare. Den delen blev fördröjd eftersom det först var oklart hur Azure-resursen skulle skapas och sedan visade det sig att kursen skulle använda en delad Document Intelligence-resurs.

Jag hade även lagt in bättre felhantering i API:t. Just nu fungerar huvudflödet, men i en riktig applikation hade jag velat hantera fel från Document Intelligence och Blob Storage mer kontrollerat och inte bara låta vissa fel bli vanliga 500-svar.

## 5. Arkitektur och ekonomi

Jag tycker att Azure Container Apps passade bra för Scanly eftersom lösningen är ganska liten och inte behöver ett helt Kubernetes-kluster. AKS hade gett mer kontroll men också gjort projektet mycket mer komplicerat.

Den största kostnaden i lösningen är Azure Document Intelligence eftersom kostnaden ökar beroende på hur många sidor som analyseras. Blob Storage och Container Registry är relativt små kostnader i jämförelse.

Med ungefär 15 000 fakturor per månad uppskattade jag Azure-kostnaden till runt 1 650–1 900 kr per månad. Vid cirka 100 000 fakturor per månad blir den ungefär 10 200–10 800 kr. Det gjorde det tydligt för mig att arkitektur inte bara handlar om vad som fungerar tekniskt, utan också om vad lösningen kostar när användningen ökar.

Den del jag ser som mest sårbar just nu är att API:t är publikt och saknar autentisering och rate limiting, så i en riktig produktionsmiljö hade jag prioriterat att lösa det först.