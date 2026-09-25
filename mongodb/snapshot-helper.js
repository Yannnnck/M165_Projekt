// Gemeinsame Vergleichsfunktion; keine Mutation der Datenbank.
globalThis.snapshot=function(database) {
    const crypto=require('crypto');
    return database.getCollectionInfos().filter(x=>x.type==='collection').sort((a,b)=>a.name.localeCompare(b.name)).map(info=>({
        name:info.name,count:database[info.name].countDocuments({}),
        sha256:crypto.createHash('sha256').update(EJSON.stringify(database[info.name].find().sort({_id:1}).toArray(),{relaxed:false})).digest('hex'),
        options:info.options,
        indexes:database[info.name].getIndexes().sort((a,b)=>a.name.localeCompare(b.name))
    }));
};
