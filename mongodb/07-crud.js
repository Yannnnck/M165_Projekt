// A5: Je zwei Beispiele pro Operation. Eigene markierte Demo-Dokumente.
const ids=[new ObjectId(),new ObjectId(),new ObjectId()];
try {
    print('CREATE: insertOne + insertMany');
    db.artists.insertOne({_id:ids[0],name:'CRUD Demo Solo',genre:'Pop'});
    db.artists.insertMany([{_id:ids[1],name:'CRUD Demo Duo',genre:'Rock'},{_id:ids[2],name:'CRUD Demo Trio',genre:'Jazz'}]);
    print('READ: findOne + find');
    printjson(db.artists.findOne({_id:ids[0]}));
    printjson(db.artists.find({_id:{$in:ids},genre:{$in:['Rock','Jazz']}}).toArray());
    print('UPDATE: updateOne + updateMany');
    printjson(db.artists.updateOne({_id:ids[0]},{$set:{genre:'Soul'}}));
    printjson(db.artists.updateMany({_id:{$in:[ids[1],ids[2]]}},{$set:{genre:'Fusion'}}));
    print('DELETE: deleteOne + deleteMany');
    printjson(db.artists.deleteOne({_id:ids[0]}));
    printjson(db.artists.deleteMany({_id:{$in:[ids[1],ids[2]]}}));
} finally { db.artists.deleteMany({_id:{$in:ids}}); }
