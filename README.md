helm upgrade --install \
  --namespace tenant-devpower \
  -f mqtttosparkplug-helm/values-devpower.yaml \
  mqtttosparkplug \
  mqtttosparkplug-helm


helm upgrade --install \
  --namespace tenant-powerco \
  -f mqtttosparkplug-helm/values-powerco.yaml \
  mqtttosparkplug \
  mqtttosparkplug-helm