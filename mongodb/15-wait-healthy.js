let healthy=false;
for(let i=0;i<60;i++) {
    const status=db.getSiblingDB('admin').runCommand({replSetGetStatus:1});
    if(status.ok&&status.members.length===3&&status.members.every(m=>m.health===1&&[1,2].includes(m.state))) {healthy=true;break;}
    sleep(1000);
}
if(!healthy) throw new Error('Nicht alle drei Knoten sind wieder gesund.');
print('PASS | Drei gesunde Knoten (Primary/Secondary).');
