#include <stdio.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/event_groups.h"
#include "esp_system.h"
#include "esp_wifi.h"
#include "esp_event_loop.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "lwip/err.h"
#include "lwip/sockets.h"
#include "lwip/sys.h"
#include <lwip/netdb.h>
#include <rom/md5_hash.h>
#include <unistd.h>
#include <cJSON.h>
#include <driver/timer.h>
#include <string.h>
#include <time.h>
#include <sys/time.h>

/*per il debug*/

//cd ~/esp/hello_world
//make flash
//make monitor

/*define constants*/

#define TIMER_COUNTDOWN 60*1000000
//0 significa timer scattato
#define TIMER_TRIGGERED   0
//dichiaro la porta su cui voglio ricevere la configurazione
#define CONFIGURATION_PORT 13000
//parametri di connessione al WIFI
#define WIFI_SSID "NotSoFastBau"
#define WIFI_PASS "Vivailpolitecnico14!"
//parametri della creazione socket
#define IP_SERVER "192.168.1.14"
#define SERVER_PORT "13000"
//maschere per poter sniffare i pacchetti
#define TYPESUBTYPE_MASK 0b0000000011111100
#define TYPE_PROBE 		 0b0000000001000000
//costante usata nella packet handler
#define MAX_SSID_LENGTH 256

/*type definitions*/

//header frame WiFi IEEE 802.11
typedef struct {
	unsigned frame_ctrl:16;
	unsigned duration_id:16;
	uint8_t addr1[6]; /* receiver address */
	uint8_t addr2[6]; /* sender address */
	uint8_t addr3[6]; /* filtering address */
	unsigned sequence_ctrl:16;
} wifi_ieee80211_mac_hdr_t;

//frame WiFi IEEE 802.11
typedef struct {
	wifi_ieee80211_mac_hdr_t hdr;
	uint8_t payload[]; /* network data ended with 4 bytes csum (CRC32) */
} wifi_ieee80211_packet_t;

//record contenete i campi di interesse dei pacchetti
typedef struct{
	char SSID[MAX_SSID_LENGTH];
	char MACADDR[18];
	int RSSI;
	char hash[33];
	uint64_t timestamp;
}record_t;

/*prototipi funzioni*/

void impostaData(time_t timestampToSet);  							//imposta la data di sistema con il timestamp ricevuto
void stampaTimestamp();												//stampa il timestamp della data di sistema attuale
int riceviConfigurazione();											//aspettiamo una configurazione della schedina dal server
void connectWIFI();													//prepara tutto per la connessione WIFI
static esp_err_t event_handler(void *ctx, system_event_t *event);	//gestisce gli eventi del WIFI e se va tutto bene si collega
void create_timer();												//creazione del timer
static void timer_callback(void* arg);  							//funzione attivata quando scatta il timer -> setta la variabile evento a 0
static void timer_task(void *arg);									//ferma lo sniffer -> apre e invia sul socket l'array JSON -> fa ripartire il timer e lo sniffing
int create_ipv4_socket_client();									//funzione che crea la connessione socket come client
void sniffaPacchetti();												//iniziamo a sniffare instanziando un packet_handler ad ogni pacchetto ricevuto
void unsetSniffaPacchetti();										//ferma lo sniffer --> gestire ancora bene il reset
void packet_handler(void *buf, wifi_promiscuous_pkt_type_t type);	//funzione richiamata ogni volta che viene ricevuto un pacchetto
char *macaddr_to_str(const uint8_t macaddr[6], char str[18]);		//converte il MAC ADDRESS in stringa
void md5(unsigned char *data, int dataLen, unsigned char *hash);	//calcola hash md5 nel pacchetto
char *hash_to_str(const unsigned char hash[16], char str[33]);		//funzione che converte il hash md5 in stringa in modo da poterlo salvare nel record
void add_json_record(record_t r);									//crea il singolo JSON relativo al pacchetto ricevuto
void json_delete();													//distrugge l'array JSON

/*global variables definition*/

//variabile per taggare i messaggi nel LOG
static const char *TAG = "SNIFFER";
//dichiarazione del timer
esp_timer_handle_t timer;
xQueueHandle timer_queue;
// Event group
static EventGroupHandle_t wifi_event_group;
const int CONNECTED_BIT = BIT0;
//flag per l'impostazione della modalita promiscus
int firstSet=1;
//dichiariamo il record
record_t record;
//socket
int sock;
//JSON
cJSON *root,*data; 		//singolo JSON
//int sniffedPackets=0; 	//numero di pacchetti sniffati mandati al termine della sessione di sniffing

// Main application
void app_main()
{
	time_t temp=1542291010;//solo debug

	//disable the default wifi logging
	esp_log_level_set("wifi", ESP_LOG_NONE);

	/*
	//ci mettiamo in ascolto sul socket TCP per ricevere una configurazione iniziale
	while((riceviConfigurazione()==-1)){
		printf("Initial configuration problem... Retrying!");
	}
	*/
	//impostiamo una certa data e ora
	impostaData(temp); //per ora a caso
	//connessione al wifi
	connectWIFI();
	//creiamo l'array di JSON
	data = cJSON_CreateArray();
	//creazione del timer
	create_timer();
	//sniffiamo qualcosa
	sniffaPacchetti();

}

void impostaData(time_t timestampToSet){
	    struct timeval now = { .tv_sec = timestampToSet };
	    settimeofday(&now, NULL);
}

void stampaTimestamp(){
	struct timeval now;
	gettimeofday(&now, NULL);
}

int riceviConfigurazione(){
	//TODO: completamente da testare. Scommentare roba nel main per farlo partire. Scrivere il codice del server.

	int no_error=1; 	//variabile che mi dice se non ci sono stati errori durante l'accept del socket
	int recv_error=0;	//se si presentano errori durante la ricezione dei dati una volta accettato il socket
	int count=0;		//conta i tentativi nel caso di errore recv

	char rx_buffer[128];
	char addr_str[128];
	int addr_family;
	int ip_protocol;

	struct sockaddr_in destAddr;
	destAddr.sin_addr.s_addr = htonl(INADDR_ANY);
	destAddr.sin_family = AF_INET;
	destAddr.sin_port = htons(CONFIGURATION_PORT);
	addr_family = AF_INET;
	ip_protocol = IPPROTO_IP;
	inet_ntoa_r(destAddr.sin_addr, addr_str, sizeof(addr_str) - 1);

	int listen_sock = socket(addr_family, SOCK_STREAM, ip_protocol);
	if (listen_sock < 0) {
		ESP_LOGE(TAG, "Unable to create socket: errno %d", errno);
		return -1;
	}
	ESP_LOGI(TAG, "Socket created");

	int err = bind(listen_sock, (struct sockaddr *)&destAddr, sizeof(destAddr));
	if (err != 0) {
		ESP_LOGE(TAG, "Socket unable to bind: errno %d", errno);
		return -1;
	}
	ESP_LOGI(TAG, "Socket binded");

	err = listen(listen_sock, 1);
	if (err != 0) {
		ESP_LOGE(TAG, "Error occured during listen: errno %d", errno);
		return -1;
	}
	ESP_LOGI(TAG, "Socket listening");

	struct sockaddr_in sourceAddr;
	uint addrLen = sizeof(sourceAddr);

	//accettazione dei socket
	while(no_error && count < 10){
		//rimetto i flag a posto
		no_error=1;
		recv_error=0;

		//faccio l'accept
		int sock = accept(listen_sock, (struct sockaddr *)&sourceAddr, &addrLen);
		if (sock < 0) {
			ESP_LOGE(TAG, "Unable to accept connection: errno %d", errno);
			no_error=0;
		}

		if(no_error){
			ESP_LOGI(TAG, "Socket accepted");
			while (1) {
				int len = recv(sock, rx_buffer, sizeof(rx_buffer) - 1, 0);
				// Error occured during receiving
				if (len < 0) {
					ESP_LOGE(TAG, "recv failed: errno %d", errno);
					recv_error=1;
					count++;
					break;
				}
				// Connection closed
				else if (len == 0) {
					ESP_LOGI(TAG, "Connection closed");
					recv_error=1;
					count++;
					break;
				}
				// Data received
				else {
				// Get the sender's ip address as string
					inet_ntoa_r(((struct sockaddr_in *)&sourceAddr)->sin_addr.s_addr, addr_str, sizeof(addr_str) - 1);

					rx_buffer[len] = 0; // Null-terminate whatever we received and treat like a string
					ESP_LOGI(TAG, "Received %d bytes from %s:", len, addr_str);
					ESP_LOGI(TAG, "%s", rx_buffer);
				}
			}


			if(recv_error){ //se ci sono stati errori nella recv
				no_error=0;	//signicica che il socket listening deve rimanere ancora in ascolto
			}

			//chiudo il socket accettato
			ESP_LOGE(TAG, "Shutting down socket");
			close(sock);
		}


	}

	if(count>=10){
		ESP_LOGE(TAG, "Superato i 10 tentativi di fare recv");
		return -1;
	}

	//chiudo il socket in ascolto
	ESP_LOGE(TAG, "Shutting down socket");
	close(listen_sock);

	return 1;
}

void connectWIFI(){
		// initialize NVS
		ESP_ERROR_CHECK(nvs_flash_init());

		// create the event group to handle wifi events
		wifi_event_group = xEventGroupCreate();

		// initialize the tcp stack
		tcpip_adapter_init();

		// initialize the wifi event handler
		ESP_ERROR_CHECK(esp_event_loop_init(event_handler, NULL));

		// initialize the wifi stack in STAtion mode with config in RAM
		wifi_init_config_t wifi_init_config = WIFI_INIT_CONFIG_DEFAULT();
		ESP_ERROR_CHECK(esp_wifi_init(&wifi_init_config));
		ESP_ERROR_CHECK(esp_wifi_set_storage(WIFI_STORAGE_RAM));
		ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));

		// configure the wifi connection and start the interface
			wifi_config_t wifi_config = {
			       .sta = {
			           .ssid = WIFI_SSID,
			           .password = WIFI_PASS,
			       },
			   };

		ESP_ERROR_CHECK(esp_wifi_set_config(ESP_IF_WIFI_STA, &wifi_config));
	    ESP_ERROR_CHECK(esp_wifi_start());
		printf("Connecting to %s\n", WIFI_SSID);

		// wait for connection
		printf("Main task: waiting for connection to the wifi network... ");
		xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT, false, true, portMAX_DELAY);

		printf("connected!\n");

		// print the local IP address
		tcpip_adapter_ip_info_t ip_info;
		ESP_ERROR_CHECK(tcpip_adapter_get_ip_info(TCPIP_ADAPTER_IF_STA, &ip_info));
		printf("IP Address:  %s\n", ip4addr_ntoa(&ip_info.ip));
		printf("Subnet mask: %s\n", ip4addr_ntoa(&ip_info.netmask));
		printf("Gateway:     %s\n", ip4addr_ntoa(&ip_info.gw));

}

static esp_err_t event_handler(void *ctx, system_event_t *event){

    switch(event->event_id) {
		case SYSTEM_EVENT_STA_START:
			esp_wifi_connect();
			break;

		case SYSTEM_EVENT_STA_GOT_IP://serve per gestire il dhcp, se usiamo gli indirizzi ip statici di può eliminare
			xEventGroupSetBits(wifi_event_group, CONNECTED_BIT);
			break;

		case SYSTEM_EVENT_STA_DISCONNECTED:
			xEventGroupClearBits(wifi_event_group, CONNECTED_BIT);
			break;

		default:
			break;
    }

	return ESP_OK;
}

void create_timer(){
	//crea una coda di parametri che verranno passati dalla callback al task effettivo
	timer_queue = xQueueCreate(10, sizeof(int));

	/* Create a one-shot timer which will fire after 10s */
	const esp_timer_create_args_t timer_args = {
			.callback = &timer_callback,
			.name = "send-timer"
	};
	ESP_ERROR_CHECK( esp_timer_create(&timer_args, &timer) );

	/* Start the timer */
	ESP_ERROR_CHECK( esp_timer_start_once(timer, TIMER_COUNTDOWN) );

	//fa partire la funzione che sarà in ascolto di eventi messi in coda dalla callback ad ogni scatto
	xTaskCreate(timer_task, "timer_evt_task", 4096, NULL, 5, NULL);
}

static void timer_callback(void* arg) {
	ESP_LOGI("TIMER", "Timer called, current val: %lld us", esp_timer_get_time());

	int evento = TIMER_TRIGGERED;

	/* Now just send the event data back to the main program task */
	xQueueSendFromISR(timer_queue, &evento, NULL);
}

static void timer_task(void *arg){
	int evento;
	char recv_buf[100]; 	//buffer in cui finiranno i dati letti in risposta dal server
	int r;					//numeri di caratteri letti dalla read ad ogni botta
	int readChars=0;		//lunghezza della stringa ricevuta, ovvero quanta roba ho messo nel recv_buf, ovvero quanti caratteri ho letto
	cJSON *serverResponse;	//JSON di risposta del server
	char *data_json;		//stringa di JSON array

	while(1){
		xQueueReceive(timer_queue, &evento, portMAX_DELAY);
		if (evento == TIMER_TRIGGERED) {
				//pezzo con la gestione dell'invio dei pacchetti

				//fermiamo lo sniffer
				unsetSniffaPacchetti();

				//creazione del socket
				sock=create_ipv4_socket_client();

				//convertiamo il JSON in stringa
				data_json = cJSON_Print(data);
				//mandiamo i dati
				if(write(sock, data_json, strlen(data_json))==strlen(data_json)){
					ets_printf("pacchetti mandati\n");
				}
				//distruggiamo subito la stringa creata
				free(data_json);

				//ricevo risposta dal server
				memset(recv_buf, 0, sizeof(recv_buf));
				readChars=0;
				do {
				  r = read(sock, recv_buf + readChars, sizeof(recv_buf) - readChars - 1);
				  readChars+=r;
				} while(r > 0);
				recv_buf[readChars]='\0';

				//passo la stringa letta ad un ogetto JSON
				serverResponse=cJSON_Parse(recv_buf);

				//chiudiamo il socket
				close(sock);
				ets_printf("socket chiuso\n");

				//imposto la dato di sistema con il nuovo valore ricevuto in risposta dal server
				impostaData(cJSON_GetObjectItem(serverResponse,"timestamp")->valueint);

				//distruggiamo l'array JSON appena mandato
				json_delete();

				//creiamo un nuovo array JSON
				data = cJSON_CreateArray();

				//facciamo ripartire lo sniffer
				sniffaPacchetti();

				//Restart the timer
				ESP_ERROR_CHECK( esp_timer_start_once(timer, TIMER_COUNTDOWN) );
				ESP_LOGI("TIMER-TASK", "Timer restarted, current val: %lld us", esp_timer_get_time());
		}
	}
}

int create_ipv4_socket_client(){
  struct addrinfo hints;
  struct addrinfo *res;
  struct in_addr *addr;

  hints.ai_family = AF_INET;
  hints.ai_socktype = SOCK_STREAM;

  int err = getaddrinfo(IP_SERVER, SERVER_PORT, &hints, &res);

  if(err != 0 || res == NULL) {
    printf("DNS lookup failed err=%d res=%p\n", err, res);
    return -1;
  }

  /* Code to print the resolved IP.

     Note: inet_ntoa is non-reentrant, look at ipaddr_ntoa_r for "real" code */
  addr = &((struct sockaddr_in *)res->ai_addr)->sin_addr;
  printf("DNS lookup succeeded. IP=%s\n", inet_ntoa(*addr));

  int l_sock = socket(res->ai_family, res->ai_socktype, 0);
  if(l_sock < 0) {
    printf("... Failed to allocate socket.\n");
    freeaddrinfo(res);
    return -1;
  }

  struct timeval to;
  to.tv_sec = 2;
  to.tv_usec = 0;
  setsockopt(l_sock,SOL_SOCKET,SO_SNDTIMEO,&to,sizeof(to));

  if(connect(l_sock, res->ai_addr, res->ai_addrlen) != 0) {
    printf("... socket connect failed errno=%d\n", errno);
    close(l_sock);
    freeaddrinfo(res);
    return -1;
  }

  printf("... connected\n");
  freeaddrinfo(res);

  // All set, socket is configured for sending and receiving
  return l_sock;
}

void sniffaPacchetti(){
	if(firstSet){
		wifi_promiscuous_filter_t filter = {.filter_mask=WIFI_PROMIS_FILTER_MASK_MGMT};
		/* enable promiscuous mode and set packet handler */
		ESP_ERROR_CHECK(esp_wifi_set_promiscuous(true));
		ESP_ERROR_CHECK(esp_wifi_set_promiscuous_filter(&filter));

		firstSet=0;
	}
	//impostiamo la funzione che verrà chiamata ad ogni pacchetto ricevuto
	ESP_ERROR_CHECK(esp_wifi_set_promiscuous_rx_cb(&packet_handler));
}

void unsetSniffaPacchetti(){
	ESP_ERROR_CHECK(esp_wifi_set_promiscuous_rx_cb(NULL));
}

void packet_handler(void *buf, wifi_promiscuous_pkt_type_t type){
	const wifi_promiscuous_pkt_t *ppkt = (wifi_promiscuous_pkt_t *) buf;
	const wifi_ieee80211_packet_t *ipkt = (wifi_ieee80211_packet_t *) ppkt->payload;
	const wifi_ieee80211_mac_hdr_t *hdr = &ipkt->hdr;
	char macaddr_str[18], ssid_str[MAX_SSID_LENGTH] = "";
	int ssid_length = 0;
	struct timeval now;

	//hash temporaneo
	unsigned char hashtmp[16];

	/* check if it is a probe request */
	if(((hdr->frame_ctrl & TYPESUBTYPE_MASK) ^ TYPE_PROBE) == 0){

		/* look for SSID in the payload */
		if(ipkt->payload[0] == 0){ // ssid field is present
			ssid_length = ipkt->payload[1];
			for(int i = 0; i < ssid_length; i++) ssid_str[i] = ipkt->payload[2+i];
			ssid_str[ssid_length] = '\0';

		}

		//Prende la data di sistema di nuovo per marcare i pacchetti
		gettimeofday(&now, NULL);

		//salvo nel record i dati scritti
		macaddr_to_str(hdr->addr2, record.MACADDR);
		memcpy(record.SSID, ssid_str, ssid_length+1);
		record.RSSI = ppkt->rx_ctrl.rssi;
		//facciamo md5 del contenuto del pacchetto
		md5((unsigned char *) ipkt, sizeof(*ipkt), hashtmp);
		hash_to_str(hashtmp, record.hash);
		//record.timestamp=ppkt->rx_ctrl.timestamp;
		record.timestamp = ((uint64_t) now.tv_sec)*1000 + now.tv_usec/1000;

		//aggiungiamo nel JSON il pacchetto sniffato
		add_json_record(record);

		//stampiamo i pacchetti sniffati
		ets_printf("TIMESTAMP s:%u ms:%u CHAN=%02d, SEQ=%4x, RSSI=%d, ADDR=%s, SSID='%s'\n",
				now.tv_sec,
				now.tv_usec/1000,
				ppkt->rx_ctrl.channel,
				hdr->sequence_ctrl,
				ppkt->rx_ctrl.rssi,
				macaddr_to_str(hdr->addr2, macaddr_str),
				ssid_str);

	}
}

char *macaddr_to_str(const uint8_t macaddr[6], char str[18]){

	sprintf(str, "%02X:%02X:%02X:%02X:%02X:%02X",
			(unsigned) macaddr[0],
			(unsigned) macaddr[1],
			(unsigned) macaddr[2],
			(unsigned) macaddr[3],
			(unsigned) macaddr[4],
			(unsigned) macaddr[5]);

	return str;
}

void md5(unsigned char *data, int dataLen, unsigned char *hash){
	struct MD5Context myContext;

	memset(&myContext,0x00,sizeof(myContext));
	memset(hash,0x00,16);

	MD5Init(&myContext);
	MD5Update(&myContext, data, dataLen);
	MD5Final(hash, &myContext);

	hash[17]='\0';
}

char *hash_to_str(const unsigned char hash[16], char str[33]){

	str[0]='\0';

	for(int i=0; i<16; i++){
		sprintf(str, "%s%02X", str, hash[i]);
	}

	return str;
}

void add_json_record(record_t r){
    root = cJSON_CreateObject();

    cJSON_AddItemToObject(root, "SSID", cJSON_CreateString(r.SSID));
    cJSON_AddItemToObject(root, "MACADDR", cJSON_CreateString(r.MACADDR));
    cJSON_AddNumberToObject(root, "RSSI", r.RSSI);
    cJSON_AddItemToObject(root, "hash", cJSON_CreateString(r.hash));
    cJSON_AddNumberToObject(root, "timestamp", r.timestamp);
    cJSON_AddItemToArray(data, root);

    return;
}

void json_delete(){
	cJSON_Delete(data);
}
