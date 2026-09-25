const port=process.env.EVENTHUB_STOP_PORT;
const admin=new Mongo(`mongodb://admin:${encodeURIComponent(process.env.EVENTHUB_ADMIN_PASSWORD)}@localhost:${port}/admin?directConnection=true&serverSelectionTimeoutMS=3000`).getDB('admin');
try {admin.runCommand({shutdown:1,force:true,timeoutSecs:2});}
catch(error) {if(error.name!=='MongoNetworkError'&&!/connection|closed|network|socket|ECONNRESET/i.test(error.message)) throw error;}
print(`Shutdown angefordert: ${port}`);
