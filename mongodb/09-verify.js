function check(condition,message) { if(!condition) throw new Error(message); print(`PASS | ${message}`); }
const minimum={events:6,locations:3,artists:10,organizers:2,customers:30,bookings:120,reviews:5};
for(const [name,n] of Object.entries(minimum)) check(db[name].countDocuments({})>=n,`${name}: Mindestanzahl ${n}`);
for(const event of db.events.find()) {
    check(!!db.locations.findOne({_id:event.locationId}) && !!db.organizers.findOne({_id:event.organizerId}),`${event.title}: Location und Veranstalter existieren`);
    check(db.artists.countDocuments({_id:{$in:event.artistIds}})===event.artistIds.length,`${event.title}: Künstler existieren`);
    const location=db.locations.findOne({_id:event.locationId});
    check(event.categories.reduce((sum,c)=>sum+Number(c.capacity),0)<=Number(location.capacity),`${event.title}: Kapazität passt zur Location`);
    for(const category of event.categories) {
        const totals=db.bookings.aggregate([
            {$match:{eventId:event._id,category:category.name,status:{$ne:'storniert'}}},
            {$group:{_id:'$status',quantity:{$sum:'$quantity'}}}
        ]).toArray();
        const total=status=>Number(totals.find(x=>x._id===status)?.quantity||0);
        check(total('bezahlt')===Number(category.sold)&&total('reserviert')===Number(category.reserved)&&
            Number(category.available)+Number(category.sold)+Number(category.reserved)===Number(category.capacity),`${event.title}/${category.name}: Bestand konsistent`);
    }
}
for(const b of db.bookings.find()) {
    if(!db.customers.findOne({_id:b.customerId}) || !db.events.findOne({_id:b.eventId,categories:{$elemMatch:{name:b.category}}}))
        throw new Error(`Verwaiste Buchung ${b._id}`);
}
check(true,'Alle Buchungsreferenzen gültig');
for(const r of db.reviews.find()) {
    const event=db.events.findOne({_id:r.eventId});
    if(!event || event.status!=='durchgeführt' || event.date>=r.createdAt || !db.customers.findOne({_id:r.customerId}) ||
       !db.bookings.findOne({eventId:r.eventId,customerId:r.customerId,status:'bezahlt'})) throw new Error(`Ungültige Bewertung ${r._id}`);
}
check(true,'Bewertungen gehören zu besuchten vergangenen Events');
const invalid=[['events',{title:'unvollständig'}],['bookings',{status:'ungültig'}],['customers',{firstName:'X',lastName:'Y',email:'keine-email'}]];
for(const [name,doc] of invalid) {
    let rejected=false;
    try {db[name].insertOne(doc);} catch(error) {if(error.code!==121) throw error;rejected=true;}
    if(!rejected) {db[name].deleteOne({_id:doc._id});throw new Error(`${name}: Validator greift nicht`);}
    check(rejected,`${name}: ungültiges Dokument mit Fehler 121 abgewiesen`);
}
print('DATEN- UND SCHEMATESTS BESTANDEN.');
