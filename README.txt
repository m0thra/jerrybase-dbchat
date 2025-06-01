# dbchat app
# need to get the right command here, so it deploys from the image
az webapp create --resource-group jerrybase-rg --plan jerrybase-plan-test --name jerrybase-dbchat --deployment-container-image-name jerrybase.azurecr.io/jerrybase-dbchat-amd:latest

# Configure the dbchat web app
az webapp config appsettings set --name jerrybase-dbchat --resource-group jerrybase-rg --settings \
  AZURE_OPENAI_API_KEY="my-key" \
  WEBSITES_PORT="8080" \
  SCM_COMMAND_IDLE_TIMEOUT="600"

# Access diagnostic logs
az webapp log config --name jerrybase-dbchat --resource-group jerrybase-rg --docker-container-logging filesystem
az webapp log tail --name jerrybase-dbchat --resource-group jerrybase-rg


# To deploy changes to prod, need to manually trigger tho, no automatic
# rev the version
docker buildx build --platform linux/amd64 --tag jerrybase-dbchat-amd .
docker tag jerrybase-dbchat-amd jerrybase.azurecr.io/jerrybase-dbchat-amd:0.1
az acr login --name jerrybase
docker push jerrybase.azurecr.io/jerrybase-dbchat-amd:0.1
# the visit the application in the UI and set it to use the new version