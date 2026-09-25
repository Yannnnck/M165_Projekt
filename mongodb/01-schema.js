// A3: Validatoren für alle sieben Collections; int und decimal bewusst getrennt.
const id = {bsonType: 'objectId'};
const text = {bsonType: 'string', minLength: 1};
const count = {bsonType: 'int', minimum: 0};
const date = {bsonType: 'date'};
const categoryNames = ['Stehplatz', 'Sitzplatz', 'VIP'];
const schemas = {
    locations: {required: ['name','address','capacity','equipment'], properties: {
        name: text, address: {bsonType:'object', required:['street','city','postalCode'], properties:{street:text,city:text,postalCode:text}},
        capacity: {bsonType:'int',minimum:1}, equipment:{bsonType:'array',items:text}
    }},
    artists: {required:['name','genre'],properties:{name:text,genre:text}},
    organizers: {required:['name','email'],properties:{name:text,email:text}},
    customers: {required:['firstName','lastName','email'],properties:{firstName:text,lastName:text,email:{bsonType:'string',pattern:'^[^@ ]+@[^@ ]+\\.[^@ ]+$'},phone:text}},
    events: {required:['title','description','date','locationId','organizerId','artistIds','status','categories'],properties:{
        title:text,description:text,date,locationId:id,organizerId:id,
        artistIds:{bsonType:'array',minItems:1,maxItems:50,uniqueItems:true,items:id},
        status:{enum:['geplant','ausverkauft','durchgeführt','abgesagt']},
        categories:{bsonType:'array',minItems:1,maxItems:3,items:{bsonType:'object',required:['name','price','capacity','available','reserved','sold'],properties:{
            name:{enum:categoryNames},price:{bsonType:'decimal',minimum:0},capacity:count,available:count,reserved:count,sold:count
        }}}
    }},
    bookings: {required:['customerId','eventId','category','quantity','unitPrice','bookedAt','status'],properties:{
        customerId:id,eventId:id,category:{enum:categoryNames},quantity:{bsonType:'int',minimum:1,maximum:10},
        unitPrice:{bsonType:'decimal',minimum:0},bookedAt:date,status:{enum:['reserviert','bezahlt','storniert']}
    }},
    reviews: {required:['customerId','eventId','rating','comment','createdAt'],properties:{
        customerId:id,eventId:id,rating:{bsonType:'int',minimum:1,maximum:5},comment:{bsonType:'string',maxLength:2000},createdAt:date
    }}
};
for (const [name, schema] of Object.entries(schemas)) {
    const json = {$jsonSchema:{bsonType:'object',...schema,properties:{_id:id,...schema.properties}}};
    let validator = json;
    if (name === 'events') validator = {$and:[json,{$expr:{$and:[
        {$eq:[{$size:'$categories'},{$size:{$setUnion:['$categories.name',[]]}}]},
        {$allElementsTrue:{$map:{input:'$categories',as:'c',in:{$eq:['$$c.capacity',{$add:['$$c.available','$$c.reserved','$$c.sold']}]}}}}
    ]}}]};
    if (!db.getCollectionNames().includes(name)) db.createCollection(name,{validator,validationLevel:'strict',validationAction:'error'});
    else { const result=db.runCommand({collMod:name,validator,validationLevel:'strict',validationAction:'error'}); if(!result.ok) throw new Error(EJSON.stringify(result)); }
}
print('Schema-Validierung: 7 Collections eingerichtet.');
