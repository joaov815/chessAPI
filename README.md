docker run --name my_postgres_container \
 -e POSTGRES_USER=myuser \
 -e POSTGRES_PASSWORD=mypassword \
 -e POSTGRES_DB=mydatabase \
 -p 5432:5432 \
 -d postgres

Fluxo

-> Pessoa x entra no site
-> tela de nome 
-> cria user com o nome POST /user
-> conecta no ws
-> 