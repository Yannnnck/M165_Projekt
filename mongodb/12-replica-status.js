const status=db.getSiblingDB('admin').runCommand({replSetGetStatus:1});
if(!status.ok) throw new Error(EJSON.stringify(status));
printjson(status.members.map(({name,stateStr,health})=>({name,stateStr,health})));
