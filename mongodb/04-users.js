const admin = db.getSiblingDB('admin');
for(const [user,password,roles] of [
    ['sales',process.env.EVENTHUB_SALES_PASSWORD,[{role:'readWrite',db:'eventhub'}]],
    ['report',process.env.EVENTHUB_REPORT_PASSWORD,[{role:'read',db:'eventhub'}]]
]) {
    if(admin.getUser(user)) admin.updateUser(user,{pwd:password,roles});
    else admin.createUser({user,pwd:password,roles});
}
printjson(admin.getUsers().users.map(({user,roles})=>({user,roles})));
