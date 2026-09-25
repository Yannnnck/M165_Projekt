if (role==='report') {
    print(`PASS | Reporting liest ${db.events.countDocuments({})} Events.`);
    let denied=false;
    try {db.artists.insertOne({name:'Unerlaubt',genre:'Test'});} catch(error) {if(error.code!==13) throw error;denied=true;}
    if(!denied) {db.artists.deleteOne({name:'Unerlaubt'});throw new Error('Reporting durfte schreiben!');}
    print('PASS | Reporting-Schreibzugriff mit Unauthorized (13) abgewiesen.');
} else if(role==='sales') {
    const id=new ObjectId();
    db.artists.insertOne({_id:id,name:'Rollentest',genre:'Test'});
    db.artists.deleteOne({_id:id});
    print('PASS | Verkauf darf Daten schreiben.');
    let denied=false;
    try {db.getSiblingDB('admin').runCommand({usersInfo:1});} catch(error) {if(error.code!==13) throw error;denied=true;}
    if(!denied) throw new Error('Verkauf darf Benutzer auflisten!');
    print('PASS | Verkauf darf Benutzer nicht auflisten.');
} else {printjson(db.getSiblingDB('admin').getUsers().users.map(x=>({user:x.user,roles:x.roles})));}
