<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.2" name="test" tilewidth="8" tileheight="8" tilecount="960" columns="32" tilerendersize="grid" fillmode="preserve-aspect-fit">
 <editorsettings>
  <export target="test..tsj" format="json"/>
 </editorsettings>
 <image source="../../Assets/Graphics/Terrain/7souls/A2_autotile_sheet.png" width="256" height="240"/>
 <tile id="0">
  <properties>
   <property name="biome" type="int" propertytype="Biome" value="0"/>
   <property name="elevation" type="float" value="0"/>
   <property name="istransparent" type="bool" value="false"/>
   <property name="layer" propertytype="Layer" value="terrain"/>
   <property name="passability" propertytype="Passability" value="passable"/>
  </properties>
 </tile>
 <tile id="12">
  <properties>
   <property name="biome" type="int" propertytype="Biome" value="0"/>
   <property name="elevation" type="float" value="0"/>
   <property name="istransparent" type="bool" value="true"/>
   <property name="layer" propertytype="Layer" value="terrain"/>
   <property name="passability" propertytype="Passability" value="passable"/>
  </properties>
 </tile>
 <tile id="204">
  <properties>
   <property name="biome" type="int" propertytype="Biome" value="0"/>
   <property name="elevation" type="float" value="0"/>
   <property name="istransparent" type="bool" value="true"/>
   <property name="layer" propertytype="Layer" value="terrain"/>
   <property name="passability" propertytype="Passability" value="passable"/>
  </properties>
 </tile>
 <wangsets>
  <wangset name="Grass3" class="Grass3" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="584" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="585" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="586" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="587" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="616" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="617" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="618" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="619" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="648" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="649" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="650" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="651" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="680" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="681" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="682" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="683" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="712" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="713" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="714" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="715" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="744" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="745" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="746" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="747" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="19"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="solid"/>
   </properties>
  </wangset>
  <wangset name="Grass2" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="580" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="581" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="582" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="583" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="612" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="613" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="614" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="615" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="644" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="645" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="646" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="647" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="676" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="677" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="678" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="679" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="708" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="709" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="710" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="711" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="740" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="741" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="742" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="743" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="388"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Grass1" type="corner" tile="497">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="576" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="577" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="578" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="579" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="608" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="609" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="610" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="611" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="640" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="641" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="642" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="643" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="672" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="673" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="674" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="675" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="704" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="705" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="706" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="707" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="736" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="737" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="738" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="739" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="616"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Mound1" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="588" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="589" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="590" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="591" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="620" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="621" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="622" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="623" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="652" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="653" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="654" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="655" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="684" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="685" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="686" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="687" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="716" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="717" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="718" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="719" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="748" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="749" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="750" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="751" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Mound2" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="592" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="593" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="594" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="595" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="624" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="625" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="626" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="627" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="656" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="657" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="658" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="659" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="688" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="689" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="690" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="691" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="720" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="721" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="722" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="723" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="752" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="753" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="754" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="755" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Mound3" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="596" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="597" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="598" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="599" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="628" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="629" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="630" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="631" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="660" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="661" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="662" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="663" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="692" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="693" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="694" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="695" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="724" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="725" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="726" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="727" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="756" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="757" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="758" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="759" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Mound4" type="corner" tile="-1">
   <wangcolor name="Foreground" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="600" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="601" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="602" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="603" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="632" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="633" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="634" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="635" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="664" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="665" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="666" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="667" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="696" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="697" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="698" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="699" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="728" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="729" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="730" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="731" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="760" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="761" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="762" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="763" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Bush1" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="604" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="605" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="606" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="607" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="636" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="637" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="638" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="639" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="668" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="669" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="670" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="671" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="700" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="701" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="702" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="703" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="732" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="733" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="734" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="735" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="764" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="765" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="766" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="767" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Rocks1" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="408" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="409" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="410" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="411" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="440" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="441" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="442" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="443" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="472" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="473" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="474" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="475" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="504" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="505" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="506" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="507" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="536" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="537" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="538" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="539" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="568" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="569" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="570" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="571" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Bush2" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="412" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="413" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="414" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="415" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="444" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="445" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="446" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="447" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="476" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="477" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="478" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="479" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="508" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="509" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="510" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="511" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="540" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="541" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="542" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="543" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="572" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="573" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="574" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="575" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Bush3" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="792" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="793" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="794" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="795" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="824" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="825" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="826" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="827" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="856" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="857" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="858" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="859" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="888" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="889" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="890" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="891" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="920" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="921" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="922" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="923" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="952" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="953" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="954" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="955" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Hedge1" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="788" wangid="0,0,1,1,1,0,0,0"/>
   <wangtile tileid="789" wangid="0,0,0,0,1,1,1,0"/>
   <wangtile tileid="790" wangid="0,1,0,1,0,1,1,0"/>
   <wangtile tileid="791" wangid="0,0,0,1,1,1,0,1"/>
   <wangtile tileid="820" wangid="1,1,1,0,0,0,0,0"/>
   <wangtile tileid="821" wangid="1,0,0,0,0,0,1,1"/>
   <wangtile tileid="822" wangid="1,1,0,1,0,0,0,1"/>
   <wangtile tileid="823" wangid="0,1,0,0,0,1,1,1"/>
   <wangtile tileid="852" wangid="0,0,1,1,1,0,0,0"/>
   <wangtile tileid="853" wangid="0,0,1,1,0,1,1,0"/>
   <wangtile tileid="854" wangid="0,0,1,1,0,1,1,0"/>
   <wangtile tileid="855" wangid="0,0,0,0,1,1,1,0"/>
   <wangtile tileid="884" wangid="1,1,0,1,1,0,0,0"/>
   <wangtile tileid="887" wangid="1,0,0,0,1,1,0,1"/>
   <wangtile tileid="916" wangid="1,1,0,1,1,0,0,0"/>
   <wangtile tileid="919" wangid="1,0,0,0,1,1,0,1"/>
   <wangtile tileid="948" wangid="1,1,1,0,0,0,0,0"/>
   <wangtile tileid="949" wangid="0,1,1,0,0,0,1,1"/>
   <wangtile tileid="950" wangid="0,1,1,0,0,0,1,1"/>
   <wangtile tileid="951" wangid="1,0,0,0,0,0,1,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Bush4" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="784" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="785" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="786" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="787" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="816" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="817" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="818" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="819" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="848" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="849" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="850" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="851" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="880" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="881" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="882" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="883" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="912" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="913" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="914" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="915" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="944" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="945" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="946" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="947" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Hedge2" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="780" wangid="0,0,1,1,1,0,0,0"/>
   <wangtile tileid="781" wangid="0,0,0,0,1,1,1,0"/>
   <wangtile tileid="782" wangid="0,1,0,1,0,1,1,0"/>
   <wangtile tileid="783" wangid="0,0,0,1,1,1,0,1"/>
   <wangtile tileid="812" wangid="1,1,1,0,0,0,0,0"/>
   <wangtile tileid="813" wangid="1,0,0,0,0,0,1,1"/>
   <wangtile tileid="814" wangid="1,1,0,1,0,0,0,1"/>
   <wangtile tileid="815" wangid="0,1,0,0,0,1,1,1"/>
   <wangtile tileid="844" wangid="0,0,1,1,1,0,0,0"/>
   <wangtile tileid="845" wangid="0,0,1,1,0,1,1,0"/>
   <wangtile tileid="846" wangid="0,0,1,1,0,1,1,0"/>
   <wangtile tileid="847" wangid="0,0,0,0,1,1,1,0"/>
   <wangtile tileid="876" wangid="1,1,0,1,1,0,0,0"/>
   <wangtile tileid="879" wangid="1,0,0,0,1,1,0,1"/>
   <wangtile tileid="908" wangid="1,1,0,1,1,0,0,0"/>
   <wangtile tileid="911" wangid="1,0,0,0,1,1,0,1"/>
   <wangtile tileid="940" wangid="1,1,1,0,0,0,0,0"/>
   <wangtile tileid="941" wangid="0,1,1,0,0,0,1,1"/>
   <wangtile tileid="942" wangid="0,1,1,0,0,0,1,1"/>
   <wangtile tileid="943" wangid="1,0,0,0,0,0,1,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Bush5" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="776" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="777" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="778" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="779" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="808" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="809" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="810" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="811" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="840" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="841" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="842" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="843" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="872" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="873" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="874" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="875" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="904" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="905" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="906" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="907" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="936" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="937" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="938" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="939" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Mound5" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="772" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="773" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="774" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="775" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="804" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="805" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="806" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="807" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="836" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="837" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="838" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="839" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="868" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="869" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="870" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="871" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="900" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="901" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="902" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="903" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="932" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="933" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="934" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="935" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Mound6" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
  <wangset name="Mound7" type="corner" tile="-1">
   <wangcolor name="" color="#ff0000" tile="-1" probability="1"/>
   <wangtile tileid="768" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="769" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="770" wangid="0,1,0,1,0,1,0,0"/>
   <wangtile tileid="771" wangid="0,0,0,1,0,1,0,1"/>
   <wangtile tileid="800" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="801" wangid="0,0,0,0,0,0,0,1"/>
   <wangtile tileid="802" wangid="0,1,0,1,0,0,0,1"/>
   <wangtile tileid="803" wangid="0,1,0,0,0,1,0,1"/>
   <wangtile tileid="832" wangid="0,0,0,1,0,0,0,0"/>
   <wangtile tileid="833" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="834" wangid="0,0,0,1,0,1,0,0"/>
   <wangtile tileid="835" wangid="0,0,0,0,0,1,0,0"/>
   <wangtile tileid="864" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="865" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="866" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="867" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="896" wangid="0,1,0,1,0,0,0,0"/>
   <wangtile tileid="897" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="898" wangid="0,1,0,1,0,1,0,1"/>
   <wangtile tileid="899" wangid="0,0,0,0,0,1,0,1"/>
   <wangtile tileid="928" wangid="0,1,0,0,0,0,0,0"/>
   <wangtile tileid="929" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="930" wangid="0,1,0,0,0,0,0,1"/>
   <wangtile tileid="931" wangid="0,0,0,0,0,0,0,1"/>
   <properties>
    <property name="InnerTerrain" value="$self"/>
    <property name="OuterTerrain" value="*"/>
    <property name="TransparentBackground" type="bool" value="true"/>
    <property name="biome" type="int" propertytype="Biome" value="0"/>
    <property name="elevation" type="float" value="0"/>
    <property name="layer" propertytype="Layer" value="terrain"/>
    <property name="passability" propertytype="Passability" value="passable"/>
   </properties>
  </wangset>
 </wangsets>
</tileset>
