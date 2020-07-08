helm upgrade --install \
    devicewifitokafka-testpower \
    devicewifitokafka-helm \
    --namespace tenant-testpower \
    --set 'image.tag=1746,tenantName=testpower,tenantNamespace=tenant-testpower,kafkaNamespace=dev' 
    