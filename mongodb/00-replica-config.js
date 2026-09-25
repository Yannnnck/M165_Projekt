// Gleiche Priorität verhindert einen unnötigen erneuten Primary-Wechsel nach Wiederanlauf.
const admin=db.getSiblingDB('admin');
const config=admin.runCommand({replSetGetConfig:1}).config;
if(config.members.some(m=>m.priority!==1)) {
    for(const member of config.members) member.priority=1;
    config.version++;
    const result=admin.runCommand({replSetReconfig:config});
    if(!result.ok) throw new Error(EJSON.stringify(result));
}
