db.artists.insertOne({_id:ObjectId(process.env.EVENTHUB_PROBE_ID),name:'Failover-Nachweis',genre:'Test'}, {writeConcern:{w:'majority'}});
print('Vor dem Ausfall: Testdokument mit majority bestätigt.');
