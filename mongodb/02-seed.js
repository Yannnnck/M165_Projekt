// Wiederholbarer Aufbau ohne bestehende Benutzerdaten zu überschreiben.
// Alle Seed-Daten werden in EINER Transaktion geschrieben: ganz oder gar nicht.
const session = db.getMongo().startSession();
const seed = session.getDatabase('eventhub');
const oid = n => ObjectId(n.toString(16).padStart(24,'0'));
const now = new Date();
const day = 86400000;
try {
    session.withTransaction(() => {
        if (seed.events.countDocuments({}) > 0) { print('Testdaten bereits vorhanden: übersprungen.'); return; }
        const locations = [
            {name:'Limmat Halle',address:{street:'Kulturweg 12',city:'Zürich',postalCode:'8005'},capacity:NumberInt(2000),equipment:['Bühne','Garderobe','Rollstuhlzugang']},
            {name:'Aare Club',address:{street:'Aareweg 8',city:'Bern',postalCode:'3000'},capacity:NumberInt(900),equipment:['Bühne','Bar']},
            {name:'Rhein Arena',address:{street:'Musikstrasse 5',city:'Basel',postalCode:'4051'},capacity:NumberInt(3000),equipment:['Bühne','Parkplätze','Rollstuhlzugang']}
        ].map((x,i)=>({_id:oid(100+i),...x}));
        seed.locations.insertMany(locations);
        seed.organizers.insertMany(['Alpine Live','Stadtklang Events'].map((name,i)=>({_id:oid(200+i),name,email:`info${i}@eventhub.example`})));
        const names = ['Neon Atlas','Luna Keller','The Riverlights','Mira Sol','Alpine Echo','Noah Frei','Velvet Tram','Sina Blue','Basel Beats','Aare Strings'];
        seed.artists.insertMany(names.map((name,i)=>({_id:oid(300+i),name,genre:['Pop','Jazz','Rock','Electro','Klassik'][i%5]})));
        const firstNames=['Lara','Noah','Mia','Luca','Emma','Leon','Nina','Tim','Sara','Jan'];
        const lastNames=['Meier','Keller','Huber'];
        seed.customers.insertMany(Array.from({length:30},(_,i)=>({_id:oid(400+i),firstName:firstNames[i%10],lastName:lastNames[Math.floor(i/10)],email:`${firstNames[i%10].toLowerCase()}.${lastNames[Math.floor(i/10)].toLowerCase()}@example.com`,phone:`+41 79 555 ${String(i).padStart(4,'0')}`})));
        const titles=['Jazz am Rhein Rückblick','Neon Pop Night','Alpine Rock Festival','Electronic River','Kammermusik Gala','Sommerklang abgesagt'];
        const events=titles.map((title,i)=>({_id:oid(500+i),title,description:`Live Musik in der Schweiz: ${title}`,date:new Date(now.getTime()+(i===0?-30:30+i*15)*day),locationId:oid(100+i%3),organizerId:oid(200+i%2),artistIds:[oid(300+(i*2)%10),oid(300+(i*2+1)%10)],status:i===0?'durchgeführt':i===5?'abgesagt':'geplant',categories:['Stehplatz','Sitzplatz','VIP'].map((name,k)=>({name,price:NumberDecimal(String(45+i*5+k*35)),capacity:NumberInt([300,200,50][k]),available:NumberInt([300,200,50][k]),reserved:NumberInt(0),sold:NumberInt(0)}))}));
        const bookings=Array.from({length:120},(_,i)=>{
            const event=events[i%6],category=event.categories[Math.floor(i/6)%3];
            const status=i%6===5||i%11===0?'storniert':i%6!==0&&i%7===0?'reserviert':'bezahlt';
            const quantity=1+i%3;
            if(status!=='storniert') {
                category.available=NumberInt(Number(category.available)-quantity);
                const counter=status==='bezahlt'?'sold':'reserved';
                category[counter]=NumberInt(Number(category[counter])+quantity);
            }
            return {_id:oid(1000+i),customerId:oid(400+i%30),eventId:event._id,category:category.name,quantity:NumberInt(quantity),unitPrice:category.price,bookedAt:new Date(now.getTime()-(60-i%20)*day),status};
        });
        // Ein vollständig ausverkauftes Event als realistischer Randfall.
        events[4].status='ausverkauft';
        for(const category of events[4].categories) {category.capacity=NumberInt(Number(category.sold)+Number(category.reserved));category.available=NumberInt(0);}
        seed.events.insertMany(events);
        seed.bookings.insertMany(bookings);
        seed.reviews.insertMany([0,6,12,18,24].map((i,k)=>({_id:oid(2000+k),customerId:oid(400+i),eventId:oid(500),rating:NumberInt(3+k%3),comment:['Tolle Atmosphäre.','Guter Sound und freundliches Team.','Sehr schönes Konzert.','Ein gelungener Abend.','Komme gerne wieder.'][k],createdAt:new Date(now.getTime()-20*day)})));
        print('Testdaten: 6 Events, 3 Locations, 10 Künstler, 30 Kunden, 120 Buchungen, 5 Bewertungen.');
    },{readConcern:{level:'snapshot'},writeConcern:{w:'majority'}});
} finally {session.endSession();}
